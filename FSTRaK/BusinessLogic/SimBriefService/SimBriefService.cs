using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using FSTRaK.BusinessLogic.FlightManager.State;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.SimBriefService
{
    /// <summary>
    /// Event-driven SimBrief integration. Fetches the user's latest OFP at two checkpoints —
    /// flight started (departure airport resolved) and taxi out — and exposes the plan when its
    /// departure matches the detected departure airport. Checkpoint 2 is the source of truth:
    /// it always fetches and replaces a checkpoint-1 match on success. No polling, no mid-flight
    /// refresh. All failures are logged and swallowed.
    /// </summary>
    internal sealed class SimBriefService : BaseModel
    {
        private static readonly object Lock = new();
        private static SimBriefService _instance;

        public static SimBriefService Instance
        {
            get
            {
                lock (Lock)
                    return _instance ??= new SimBriefService();
            }
        }

        private SimBriefService() { }

        private FlightManager.FlightManager _flightManager;
        private Flight _subscribedFlight;
        private bool _checkpoint2Done;
        private bool _checkpoint2Fetched;

        private FlightPlan _matchedFlightPlan;
        public FlightPlan MatchedFlightPlan
        {
            get => _matchedFlightPlan;
            private set
            {
                if (ReferenceEquals(_matchedFlightPlan, value)) return;
                _matchedFlightPlan = value;
                OnPropertyChanged();
            }
        }

        public void Initialize()
        {
            _flightManager = FlightManager.FlightManager.Instance;
            _flightManager.PropertyChanged += FlightManagerOnPropertyChanged;
        }

        private void FlightManagerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FlightManager.FlightManager.ActiveFlight):
                    // Raised on every FlightData tick — only rewire when the flight instance changed.
                    if (ReferenceEquals(_subscribedFlight, _flightManager.ActiveFlight)) return;
                    if (_subscribedFlight != null)
                        _subscribedFlight.PropertyChanged -= FlightOnPropertyChanged;
                    _subscribedFlight = _flightManager.ActiveFlight;
                    if (_subscribedFlight != null)
                        _subscribedFlight.PropertyChanged += FlightOnPropertyChanged;
                    break;

                case nameof(FlightManager.FlightManager.State):
                    OnStateChanged();
                    break;
            }
        }

        private void OnStateChanged()
        {
            switch (_flightManager.State)
            {
                case FlightStartedState _:
                case SimNotInFlightState _:
                    Reset();
                    break;
                case TaxiOutState _ when !_checkpoint2Done:
                    _checkpoint2Done = true;
                    _ = FetchAndMatchAsync("checkpoint 2 (taxi out)", isCheckpoint2: true);
                    break;
            }
        }

        private void FlightOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Flight.DepartureAirport)) return;
            if (string.IsNullOrEmpty(_subscribedFlight?.DepartureAirport)) return;
            // Late resolution: checkpoint 2 fired before the departure airport was known — run it now.
            if (_checkpoint2Done && !_checkpoint2Fetched)
            {
                _ = FetchAndMatchAsync("checkpoint 2 (departure resolved late)", isCheckpoint2: true);
                return;
            }
            // Checkpoint 1 — the departure airport resolves asynchronously after FlightStartedState is entered.
            if (_flightManager.State is not FlightStartedState) return;
            if (_checkpoint2Done || MatchedFlightPlan != null) return;
            _ = FetchAndMatchAsync("checkpoint 1 (flight started)", isCheckpoint2: false);
        }

        private void Reset()
        {
            _checkpoint2Done = false;
            _checkpoint2Fetched = false;
            MatchedFlightPlan = null;
        }

        private async Task FetchAndMatchAsync(string checkpoint, bool isCheckpoint2)
        {
            var user = Properties.Settings.Default.SimbriefUser?.Trim();
            if (string.IsNullOrEmpty(user)) return;
            var departure = _flightManager.ActiveFlight?.DepartureAirport;
            if (string.IsNullOrEmpty(departure)) return;

            if (isCheckpoint2)
                _checkpoint2Fetched = true;

            try
            {
                Log.Information($"SimBrief: fetching latest OFP at {checkpoint}");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var json = await client.GetStringAsync(SimBriefOfpMapper.BuildFetchUrl(user));

                var ofp = SimBriefOfpMapper.Parse(json);
                if (ofp == null)
                {
                    Log.Information("SimBrief: no valid OFP on file");
                    return;
                }

                var plan = SimBriefOfpMapper.Map(ofp);
                if (plan == null)
                {
                    Log.Warning("SimBrief: OFP could not be mapped (missing airports or navlog)");
                    return;
                }

                if (!SimBriefOfpMapper.MatchesDeparture(plan, departure))
                {
                    Log.Information($"SimBrief: OFP departure {plan.DepartureAirport} does not match {departure} - ignoring");
                    return;
                }

                Log.Information($"SimBrief: matched plan {plan.DepartureAirport} -> {plan.ArrivalAirport} at {checkpoint}");
                MatchedFlightPlan = plan;
            }
            catch (Exception ex)
            {
                // On a checkpoint-2 failure a checkpoint-1 match is intentionally kept.
                Log.Warning(ex, "SimBrief: failed to fetch or parse OFP");
            }
        }
    }
}
