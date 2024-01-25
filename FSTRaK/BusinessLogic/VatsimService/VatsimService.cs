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

        private VatsimService()
        {
            _connectionTimer = new Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetVatsimData();
            _connectionTimer.AutoReset = true;
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
