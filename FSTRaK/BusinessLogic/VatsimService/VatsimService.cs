using FSTRaK.BusinessLogic.SimconnectService;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.Models;
using Serilog;
using MapControl;
using System.IO;
using System.Reflection;
using static FSTRaK.BusinessLogic.VatsimService.VatsimModel.VatsimStaticData;

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
            set;
        }

        private VatsimService()
        {
            VatsimStaticData = new VatsimStaticData();
            _connectionTimer = new Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetVatsimData();
            _connectionTimer.AutoReset = true;
            ParseStaticData();
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
                                    IsPseudo = Int32.Parse(columns[6].Substring(0,1)) != 0
                                };
                                VatsimStaticData.Airports.Add(airport);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, line );
                            }

                            line = reader.ReadLine();

                        } while (line is not "[FIRs]");
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


    }
}
