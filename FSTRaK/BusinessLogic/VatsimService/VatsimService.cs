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

        private Dictionary<string, ControlledAirport> _controlledAirports;
        public Dictionary<string, ControlledAirport> ControlledAirports
        {
            get => _controlledAirports;
            private set
            {
                if (value != _controlledAirports)
                {
                    _controlledAirports = value;
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
            ControlledAirports = new Dictionary<string, ControlledAirport>();
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
            using (StreamReader reader = new StreamReader($@"{System.AppContext.BaseDirectory}\Resources\Data\VATSpy.dat"))
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
                                centerName = columns.Length == 3 && columns[2].Equals(string.Empty)
                                    ? columns[2]
                                    : "Center"
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
                        }
                    }


                    // Handle additional columns if needed
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
                        VatsimData = JsonConvert.DeserializeObject<VatsimData>(jsonContent);
                        BuildControlledAirportsList();
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

        private void BuildControlledAirportsList()
        {
            ControlledAirports.Clear();
            foreach (var controller in VatsimData.controllers)
            {
                if (controller.facility == 2 || controller.facility == 3 || controller.facility == 4 || controller.facility == 5)
                {
                    // Find airport
                    var callsignParts = controller.callsign.Split('_');
                    if (ControlledAirports.ContainsKey(callsignParts[0]))
                    {
                        var airport = ControlledAirports[callsignParts[0]];
                        airport.Controllers.Add(controller);
                    }
                    else
                    {
                        var airport = VatsimStaticData.Airports.Find(a => a.ICAO.Equals(callsignParts[0]));
                        if (airport != null)
                        {
                            var controlledAirport = new ControlledAirport(airport);
                            controlledAirport.Controllers.Add(controller);
                            ControlledAirports.Add(controlledAirport.Airport.ICAO, controlledAirport);
                        }
                    }
                }
            }
            OnPropertyChanged(nameof(ControlledAirports));
        }

        public (double[] labelCoordinates, double[][] coordinates) GetFirBoundariesByController(Controller controller)
        {

            var prefix = controller.callsign.Substring(0, controller.callsign.LastIndexOf('_'));
            var firBoundary = VatsimStaticData.FIRs.Find(f => f.CallsignPrefix.Equals(prefix));
            if (firBoundary == null)
            {
                firBoundary = VatsimStaticData.FIRs.Find(f => f.CallsignPrefix.Equals(prefix.Split('_')[0]));
            }
            if (firBoundary == null)
            {
                firBoundary = VatsimStaticData.FIRs.Find(f => f.ICAO.Equals(prefix.Split('_')[0]));
            }

            if (firBoundary == null)
            {
                throw new Exception("No FIR was found for " + controller.callsign);
            }

            var fir = FirBoundaries.Features.FirstOrDefault(feature => feature.Properties.id.Equals(firBoundary.Boundary));
            if (fir != null)
            {
                return (new double[] { Double.Parse(fir.Properties.label_lat), Double.Parse(fir.Properties.label_lon) }, fir.Geometry.Coordinates[0][0]);
            }

            throw new Exception("No FIR was found for " + controller.callsign);
        }
    }
}
