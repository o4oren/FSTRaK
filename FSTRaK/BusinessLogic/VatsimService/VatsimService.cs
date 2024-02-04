using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.Models;
using Serilog;
using System.IO;
using MapControl;


namespace FSTRaK.BusinessLogic.VatsimService
{
    internal class VatsimService : BaseModel
    {
        private Timer _connectionTimer;
        private const int ConnectionInterval = 20 * 1000;
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

        private VatsimService()
        {
            VatsimStaticData = new VatsimStaticData();
            _connectionTimer = new Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetVatsimData();
            _connectionTimer.AutoReset = true;
            ParseStaticData();
            ParseBoundariesGeoJson();
        }

        private void ParseBoundariesGeoJson()
        {
            string filePath = $@"{System.AppContext.BaseDirectory}\Resources\Data\Boundaries.geojson";

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                GeoJsonFeatureCollection featureCollection = JsonConvert.DeserializeObject<GeoJsonFeatureCollection>(jsonContent);
                FirBoundaries = featureCollection;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading or parsing GeoJSON file: {ex.Message}");
            }
        }

        private void ParseStaticData()
        {
            using (StreamReader reader =
                   new StreamReader($@"{System.AppContext.BaseDirectory}\Resources\Data\VATSpy.dat"))
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
                            VatsimStaticData.Countries.Add(country.Initials, country);
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
                                VatsimStaticData.Airports.Add(airport);
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
                                    VatsimStaticData.FIRs.Add(fir);
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
                                        VatsimStaticData.UIRs.Add(uir);
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
        }

        public void Stop()
        {
            Log.Information("Stopping to poll VATSIM");
            VatsimData = null;
            _connectionTimer.Stop();
        }

        private async Task GetVatsimData()
        {
            try
            {
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
            var prefix = controller.callsign.Substring(0, controller.callsign.LastIndexOf('_'));
            var prefixIcaoCandidate = controller.callsign.Split(('_'))[0];

            var firBoundary = VatsimStaticData.FIRs.FindAll(f => f.CallsignPrefix.Equals(prefix));
            if (firBoundary.Count == 0)
            {
                firBoundary = VatsimStaticData.FIRs.FindAll(f => f.ICAO.Equals(prefixIcaoCandidate));
            }
            if (firBoundary.Count == 0)
            {
                firBoundary = VatsimStaticData.FIRs.FindAll(f => f.CallsignPrefix.Equals(prefixIcaoCandidate));
            }


            if (firBoundary.Count == 0)
            {
                throw new Exception("No FIR was found for " + controller.callsign);
            }

            string postfix = controller.callsign.Split('_').LastOrDefault();
            string oceanic = postfix is "FSS" ? "1" : "0";

            GeoJsonFeature fir;
            if (!firBoundary[0].Boundary.Equals(string.Empty))
            {
                fir = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(firBoundary[0].Boundary) && feature.Properties.oceanic.Equals(oceanic));
            }
            else
            {
                fir = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(firBoundary[0].ICAO) && feature.Properties.oceanic.Equals(oceanic));
            }

            
            if (fir != null)
            {
                var country =
                    VatsimStaticData.Countries.FirstOrDefault(c =>
                        c.Value.Initials.Equals(fir.Properties.id.Substring(0, 2)));
                var centerName = country.Value != null ? country.Value.centerName : "Radar";
                return (new Location( Double.Parse(fir.Properties.label_lat), Double.Parse(fir.Properties.label_lon) ), fir.Geometry.Coordinates, firBoundary[0].Name + " " + centerName);
            }

            throw new Exception("No FIR was found for " + controller.callsign);
        }

        public List<(Location labelCoordinates, double[][][][] coordinates, string firName)> GetBoundariesArrayByController(Controller controller)
        {
            var prefix = controller.callsign.Substring(0, controller.callsign.LastIndexOf('_'));
            var firs = new List<(Location labelCoordinates, double[][][][] coordinates, string firName)>();
            var uirs = VatsimStaticData.UIRs.FindAll(u => u.CallsignPrefix.Equals(prefix));
            if (uirs.Count > 0)
            {
                foreach (var fir in uirs[0].Firs)
                {
                    var firBoundaries =
                        FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(fir));
                    if (fir != null)
                    {
                        var country =
                            VatsimStaticData.Countries.FirstOrDefault(c =>
                                c.Value.Initials.Equals(firBoundaries.Properties.id.Substring(0, 2)));
                        firs.Add((new Location(Double.Parse(firBoundaries.Properties.label_lat), Double.Parse(firBoundaries.Properties.label_lon)), firBoundaries.Geometry.Coordinates, uirs[0].Name));
                    }
                }
            }
            return firs;
        }
    }
}
