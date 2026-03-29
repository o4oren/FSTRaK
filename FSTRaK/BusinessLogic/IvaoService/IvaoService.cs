using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.Models;
using Newtonsoft.Json;
using Serilog;

namespace FSTRaK.BusinessLogic.IvaoService
{
    internal class IvaoService : BaseModel
    {
        private const string PilotsUrl = "https://api.ivao.aero/v2/tracker/now/pilots/summary";
        private const string AtcUrl = "https://api.ivao.aero/v2/tracker/now/atc/summary";
        private const int ConnectionInterval = 60 * 1000;

        private System.Timers.Timer _connectionTimer;

        public bool Started { get; private set; }

        private IvaoData _ivaoData;
        public IvaoData IvaoData
        {
            get => _ivaoData;
            private set
            {
                if (value != _ivaoData)
                {
                    _ivaoData = value;
                    OnPropertyChanged();
                }
            }
        }

        private static readonly object Lock = new();
        private static IvaoService _instance;
        public static IvaoService Instance
        {
            get
            {
                lock (Lock)
                {
                    return _instance ??= new IvaoService();
                }
            }
        }

        private IvaoService()
        {
            _connectionTimer = new System.Timers.Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetIvaoData();
            _connectionTimer.AutoReset = true;
        }

        public async void Start()
        {
            Log.Information("Starting to poll IVAO for data");
            await GetIvaoData();
            _connectionTimer.Start();
            Started = true;
        }

        public void Stop()
        {
            Log.Information("Stopping IVAO polling");
            IvaoData = null;
            _connectionTimer.Stop();
            Started = false;
        }

        private async Task GetIvaoData()
        {
            try
            {
                Log.Debug("Fetching IVAO data");
                using var client = new HttpClient();

                var pilotsTask = client.GetStringAsync(PilotsUrl);
                var atcTask = client.GetStringAsync(AtcUrl);
                await Task.WhenAll(pilotsTask, atcTask);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var data = new IvaoData
                    {
                        pilots = JsonConvert.DeserializeObject<System.Collections.Generic.List<IvaoPilot>>(pilotsTask.Result),
                        atcEntries = JsonConvert.DeserializeObject<System.Collections.Generic.List<IvaoAtcEntry>>(atcTask.Result)
                    };
                    IvaoData = data;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while calling IVAO");
            }
        }
    }
}
