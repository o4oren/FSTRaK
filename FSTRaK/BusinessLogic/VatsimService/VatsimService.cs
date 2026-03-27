using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.Models;
using Serilog;
using System.IO;
using MapControl;
using FSTRaK.Utils;


namespace FSTRaK.BusinessLogic.VatsimService
{
    internal class VatsimService : BaseModel
    {
        private System.Timers.Timer _connectionTimer;
        private const int ConnectionInterval = 60 * 1000;

        public bool Started
        {
            get;
            private set;
        }

        private VatsimData _vatsimData;

        public VatsimData VatsimData
        {
            get => _vatsimData;
            private set
            {
                if (value != _vatsimData)
                {
                    _vatsimData = value;
                    OnPropertyChanged();
                }
            }
        }

        public VatsimStaticData VatsimStaticData
        {
            get;
            private set;
        }

        public GeoJsonFeatureCollection FirBoundaries
        {
            get;
            private set;
        }

        private Dictionary<string, List<LocationCollection>> _traconByPrefix = new();
        private Dictionary<string, List<LocationCollection>> _traconByPrefixAndSuffix = new();

        // Protects VatsimStaticData, FirBoundaries, and the TRACON dictionaries against
        // concurrent reads (VATSIM timer thread) and ReloadDataFiles (background thread).
        private readonly ReaderWriterLockSlim _dataLock = new ReaderWriterLockSlim();

        private static string DataDir => Path.Combine(PathUtil.GetApplicationLocalDataPath(), "Data");

        private VatsimService()
        {
            VatsimStaticData = new VatsimStaticData();
            _connectionTimer = new System.Timers.Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetVatsimData();
            _connectionTimer.AutoReset = true;
            // Constructor runs single-threaded; no lock needed here.
            ParseStaticDataInto(VatsimStaticData);
            FirBoundaries = ParseBoundariesGeoJsonInto();
            ParseTraconBoundariesGeoJsonInto(_traconByPrefix, _traconByPrefixAndSuffix);
        }

        private GeoJsonFeatureCollection ParseBoundariesGeoJsonInto()
        {
            string filePath = Path.Combine(DataDir, "Boundaries.geojson");

            if (!File.Exists(filePath))
            {
                Log.Warning("Boundaries.geojson not found at {Path} — FIR boundaries unavailable until download completes", filePath);
                return null;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<GeoJsonFeatureCollection>(jsonContent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading or parsing Boundaries.geojson");
                return null;
            }
        }

        private void ParseTraconBoundariesGeoJsonInto(
            Dictionary<string, List<LocationCollection>> byPrefix,
            Dictionary<string, List<LocationCollection>> byPrefixAndSuffix)
        {
            string filePath = Path.Combine(DataDir, "TRACONBoundaries.geojson");

            if (!File.Exists(filePath))
            {
                Log.Warning("TRACONBoundaries.geojson not found at {Path} — TRACON polygons unavailable", filePath);
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var root = JObject.Parse(jsonContent);
                var features = root["features"] as JArray;
                if (features == null) return;

                foreach (var feature in features)
                {
                    var props = feature["properties"];
                    if (props == null) continue;

                    // prefix can be a string or an array of strings
                    var prefixes = new List<string>();
                    var prefixToken = props["prefix"];
                    if (prefixToken?.Type == JTokenType.String)
                        prefixes.Add(prefixToken.Value<string>());
                    else if (prefixToken?.Type == JTokenType.Array)
                        prefixes.AddRange(prefixToken.Values<string>());

                    if (prefixes.Count == 0) continue;

                    var suffix = props["suffix"]?.Value<string>();
                    var geometry = feature["geometry"];
                    var geoType = geometry?["type"]?.Value<string>();
                    var coordsToken = geometry?["coordinates"];
                    if (coordsToken == null) continue;

                    // Build one LocationCollection per polygon outer ring
                    var polygons = BuildTraconPolygons(geoType, coordsToken);
                    if (polygons.Count == 0) continue;

                    foreach (var prefix in prefixes)
                    {
                        if (string.IsNullOrEmpty(prefix)) continue;

                        if (!string.IsNullOrEmpty(suffix))
                        {
                            var key = $"{prefix}_{suffix}";
                            if (!byPrefixAndSuffix.ContainsKey(key))
                                byPrefixAndSuffix[key] = new List<LocationCollection>();
                            byPrefixAndSuffix[key].AddRange(polygons);
                        }
                        else
                        {
                            if (!byPrefix.ContainsKey(prefix))
                                byPrefix[prefix] = new List<LocationCollection>();
                            byPrefix[prefix].AddRange(polygons);
                        }
                    }
                }

                Log.Information("TRACON boundaries loaded: {PrefixCount} prefix entries, {SuffixCount} prefix+suffix entries",
                    byPrefix.Count, byPrefixAndSuffix.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading or parsing TRACONBoundaries.geojson");
            }
        }

        private static List<LocationCollection> BuildTraconPolygons(string geoType, JToken coordsToken)
        {
            var result = new List<LocationCollection>();
            try
            {
                if (geoType == "Polygon")
                {
                    // coords = [ring][point] — take outer ring (index 0)
                    var outerRing = coordsToken[0];
                    var lc = JRingToLocationCollection(outerRing);
                    if (lc.Count > 0) result.Add(lc);
                }
                else if (geoType == "MultiPolygon")
                {
                    // coords = [polygon][ring][point]
                    foreach (var polygon in coordsToken)
                    {
                        var outerRing = polygon[0];
                        var lc = JRingToLocationCollection(outerRing);
                        if (lc.Count > 0) result.Add(lc);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse TRACON polygon geometry (type: {Type})", geoType);
            }
            return result;
        }

        private static LocationCollection JRingToLocationCollection(JToken ring)
        {
            var lc = new LocationCollection();
            foreach (var coord in ring)
                lc.Add(new Location(coord[1].Value<double>(), coord[0].Value<double>()));
            return lc;
        }

        /// <summary>
        /// Returns the TRACON polygon(s) for the given callsign prefix and suffix.
        /// Checks prefix+suffix first (e.g. "ATL_DEP"), falls back to prefix-only ("ATL").
        /// Returns empty list when no polygon is found.
        /// </summary>
        public List<LocationCollection> GetTraconPolygons(string prefix, string suffix)
        {
            _dataLock.EnterReadLock();
            try
            {
                if (!string.IsNullOrEmpty(suffix))
                {
                    var key = $"{prefix}_{suffix}";
                    if (_traconByPrefixAndSuffix.TryGetValue(key, out var suffixResult))
                        return suffixResult;
                }

                if (_traconByPrefix.TryGetValue(prefix, out var prefixResult))
                    return prefixResult;

                return new List<LocationCollection>();
            }
            finally
            {
                _dataLock.ExitReadLock();
            }
        }

        private void ParseStaticDataInto(VatsimStaticData target)
        {
            string filePath = Path.Combine(DataDir, "VATSpy.dat");

            if (!File.Exists(filePath))
            {
                Log.Warning("VATSpy.dat not found at {Path}", filePath);
                return;
            }

            using (StreamReader reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line is "[Countries]")
                    {
                        line = reader.ReadLine();
                        do
                        {
                            if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                            {
                                line = reader.ReadLine();
                                continue;
                            }

                            string[] columns = line.Split('|');

                            var country = new VatsimStaticData.Country
                            {
                                Name = columns[0],
                                Initials = columns[1],
                                centerName = columns[2].Equals(string.Empty)
                                    ? "Center"
                                    : columns[2]
                            };
                            target.Countries.Add(country.Initials, country);
                            line = reader.ReadLine();
                        } while (!(line is "[Airports]"));
                    }

                    if (line is "[Airports]")
                    {
                        line = reader.ReadLine();
                        do
                        {
                            if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                            {
                                line = reader.ReadLine();
                                continue;
                            }

                            string[] columns = line.Split('|');

                            try
                            {
                                var airport = new VatsimStaticData.Airport()
                                {
                                    ICAO = columns[0],
                                    Name = columns[1],
                                    Latitude = Double.Parse(columns[2]),
                                    Longitude = Double.Parse(columns[3]),
                                    IATA = columns[4],
                                    FIR = columns[5],
                                    IsPseudo = Int32.Parse(columns[6].Substring(0, 1)) != 0
                                };
                                target.Airports.Add(airport);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, line);
                            }

                            line = reader.ReadLine();

                        } while (line is not "[FIRs]");


                        if (line is "[FIRs]")
                        {
                            line = reader.ReadLine();
                            do
                            {
                                if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                                {
                                    line = reader.ReadLine();
                                    continue;
                                }

                                string[] columns = line.Split('|');

                                try
                                {
                                    var fir = new VatsimStaticData.FIR()
                                    {
                                        ICAO = columns[0],
                                        Name = columns[1],
                                        CallsignPrefix = columns[2],
                                        Boundary = columns[3],
                                    };
                                    target.FIRs.Add(fir);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, line);
                                }

                                line = reader.ReadLine();

                            } while (line is not "[UIRs]");
                            if (line is "[UIRs]")
                            {
                                line = reader.ReadLine();
                                do
                                {
                                    if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                                    {
                                        line = reader.ReadLine();
                                        continue;
                                    }

                                    string[] columns = line.Split('|');

                                    try
                                    {
                                        var uir = new VatsimStaticData.UIR()
                                        {
                                            CallsignPrefix = columns[0],
                                            Name = columns[1],
                                            Firs = new List<string>(columns[2].Split(',')),
                                        };
                                        target.UIRs.Add(uir);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, line);
                                    }

                                    line = reader.ReadLine();

                                } while (line is not "[IDL]");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reloads all data files from disk. Called by DataFileUpdateService after downloads complete.
        /// Safe to call from any thread.
        /// </summary>
        public void ReloadDataFiles()
        {
            Log.Information("Reloading VATSIM static data files");

            // Build all new data structures off-lock so the lock hold time is minimal.
            var newStaticData = new VatsimStaticData();
            GeoJsonFeatureCollection newFirBoundaries = null;
            var newTraconByPrefix = new Dictionary<string, List<LocationCollection>>();
            var newTraconByPrefixAndSuffix = new Dictionary<string, List<LocationCollection>>();

            ParseStaticDataInto(newStaticData);
            newFirBoundaries = ParseBoundariesGeoJsonInto();
            ParseTraconBoundariesGeoJsonInto(newTraconByPrefix, newTraconByPrefixAndSuffix);

            // Atomically publish all new references under write lock.
            _dataLock.EnterWriteLock();
            try
            {
                VatsimStaticData = newStaticData;
                FirBoundaries = newFirBoundaries;
                _traconByPrefix = newTraconByPrefix;
                _traconByPrefixAndSuffix = newTraconByPrefixAndSuffix;
            }
            finally
            {
                _dataLock.ExitWriteLock();
            }
        }

        private static readonly object Lock = new();
        private static VatsimService _instance;

        public static VatsimService Instance
        {
            get
            {
                lock (Lock)
                {
                    return _instance ??= new VatsimService();
                }
            }
        }

        public async void Start()
        {
            Log.Information("Starting to poll VATSIM for Data");
            await GetVatsimData();
            _connectionTimer.Start();
            Started = true;
        }

        public void Stop()
        {
            Log.Information("Stopping to poll VATSIM");
            VatsimData = null;
            _connectionTimer.Stop();
            Started = false;
        }

        private async Task GetVatsimData()
        {
            try
            {
                Log.Debug("Fetching Vatsim Data");
                using HttpClient client = new HttpClient();
                string apiUrl = "https://data.vatsim.net/v3/vatsim-data.json";
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        VatsimData data = JsonConvert.DeserializeObject<VatsimData>(jsonContent);

                        //code for adding a controller to the list for debugging
//                        Controller c = new Controller();
//                        c.callsign = "EUC-ME_FSS";
//                        c.facility = 1;
//                        c.cid = 123;
//                        c.name = "Oren";
//                        c.frequency = "199.9";
//                        c.logon_time = "2024-01-28T20:17:29.1405912Z";
//                        data.controllers.Add(c);


                        VatsimData = data;
                    });

                }
                else
                {
                    Log.Error($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while calling VATSIM");
            }
        }

        public (Location labelCoordinates, double[][][][] coordinates, string firName) GetFirBoundariesByController(Controller controller)
        {
            _dataLock.EnterReadLock();
            try
            {
                var prefix = controller.callsign.Substring(0, controller.callsign.LastIndexOf('_'));
                var prefixIcaoCandidate = controller.callsign.Split(('_'))[0];

                var firBoundary = VatsimStaticData.FIRs.FindAll(f => f.CallsignPrefix.Equals(prefix));
                if (firBoundary.Count == 0)
                    firBoundary = VatsimStaticData.FIRs.FindAll(f => f.ICAO.Equals(prefixIcaoCandidate));
                if (firBoundary.Count == 0)
                    firBoundary = VatsimStaticData.FIRs.FindAll(f => f.CallsignPrefix.Equals(prefixIcaoCandidate));

                if (firBoundary.Count == 0)
                    throw new Exception("No FIR was found for " + controller.callsign);

                string postfix = controller.callsign.Split('_').LastOrDefault();
                string oceanic = postfix is "FSS" ? "1" : "0";

                GeoJsonFeature fir;
                if (!firBoundary[0].Boundary.Equals(string.Empty))
                    fir = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(firBoundary[0].Boundary) && feature.Properties.oceanic.Equals(oceanic));
                else
                    fir = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(firBoundary[0].ICAO) && feature.Properties.oceanic.Equals(oceanic));

                if (fir != null)
                {
                    var country = VatsimStaticData.Countries.FirstOrDefault(c =>
                        c.Value.Initials.Equals(fir.Properties.id.Substring(0, 2)));
                    var centerName = country.Value != null ? country.Value.centerName : "Radar";
                    return (new Location(Double.Parse(fir.Properties.label_lat), Double.Parse(fir.Properties.label_lon)), fir.Geometry.Coordinates, firBoundary[0].Name + " " + centerName);
                }

                throw new Exception("No FIR was found for " + controller.callsign);
            }
            finally
            {
                _dataLock.ExitReadLock();
            }
        }

        public List<(Location labelCoordinates, double[][][][] coordinates, string firName)> GetBoundariesArrayByController(Controller controller)
        {
            _dataLock.EnterReadLock();
            try
            {
                var prefix = controller.callsign.Substring(0, controller.callsign.LastIndexOf('_'));
                var firs = new List<(Location labelCoordinates, double[][][][] coordinates, string firName)>();
                var uirs = VatsimStaticData.UIRs.FindAll(u => u.CallsignPrefix.Equals(prefix));
                if (uirs.Count > 0)
                {
                    foreach (var fir in uirs[0].Firs)
                    {
                        var firBoundaries = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(fir));
                        if (fir != null)
                        {
                            var country = VatsimStaticData.Countries.FirstOrDefault(c =>
                                c.Value.Initials.Equals(firBoundaries.Properties.id.Substring(0, 2)));
                            firs.Add((new Location(Double.Parse(firBoundaries.Properties.label_lat), Double.Parse(firBoundaries.Properties.label_lon)), firBoundaries.Geometry.Coordinates, uirs[0].Name));
                        }
                    }
                }
                return firs;
            }
            finally
            {
                _dataLock.ExitReadLock();
            }
        }
    }
}
