using FSTRaK.DataTypes;
using Serilog;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FSTRaK.Models.FlightManager
{
    /// <summary>
    /// FlightManager is the domain model managing the flight. It's responsibilities are to subscribe to the Simconnect service events, 
    /// manage the flight state, expose the model data to realtime map view and persist the flight when they end.
    /// </summary>
    internal sealed class FlightManager : INotifyPropertyChanged
    {
        private static readonly object _lock = new object();
        private static FlightManager instance = null;
        private FlightManager() { }

        public event PropertyChangedEventHandler PropertyChanged;
        private SimConnectService _simConnectService;
        public static FlightManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new FlightManager();
                    }
                    return instance;
                }
            }
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
            _simConnectService.PropertyChanged += SimconnectService_OnPropertyChange;
            State = new SimNotInFlightState(this);
        }

        // Properties
        private Flight _activeFlight;
        public Flight ActiveFlight
        {
            get { return _activeFlight; }
            set
            {
                if (value != _activeFlight)
                {
                    _activeFlight = value;
                    OnPropertyChanged();
                }
            }
        }

        private FlightParams _currentFlightParams;
        public FlightParams CurrentFlightParams
        {
            get { return _currentFlightParams; }
            set
            {
                _currentFlightParams = value;
                OnPropertyChanged();
            }
        }

        private AbstractState _state;
        internal AbstractState State { 
            get { return _state; }
            set
            {
                _state = value;
                Log.Information($"State changed - {DateTime.Now} - {value.Name}");
                OnPropertyChanged();
            }
        }

        private bool _simConnectInFlight = false;
        public bool SimConnectInFlight { get { return _simConnectInFlight; } set { if( _simConnectInFlight == value ) return; _simConnectInFlight = value; OnPropertyChanged(); } }

        private NearestAirportRequestType _nearestAirportRequestType = NearestAirportRequestType.Departure;

        private void SimconnectService_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {            
            switch(e.PropertyName)
            {
                case nameof(SimConnectService.IsCrashed):
                    if(_simConnectService.IsCrashed)
                    {
                        State = new CrashedState(this);
                    }
                    break;
                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;

                    _state.ProcessFlightData(data);

                    // Updating the map in realtime if not in non-flight states
                    if (!(State is SimNotInFlightState))
                    {
                        FlightParams fp = new FlightParams();
                        fp.TrueAirspeed = data.TrueAirspeed;
                        fp.Heading = data.TrueHeading;
                        fp.IsOnGround = Convert.ToBoolean(data.SimOnGround);
                        fp.Latitude = data.Latitude;
                        fp.Longitude = data.Longitude;
                        fp.Altitude = data.Altitude;
                        CurrentFlightParams = fp;
                    }

                    OnPropertyChanged("ActiveFlight");
                    break;

                case nameof(SimConnectService.NearestAirport):
                    var airport = _simConnectService.NearestAirport;
                    if(ActiveFlight != null && CurrentFlightParams.IsOnGround)
                    {
                        Log.Debug($"xxx {airport} requested for {NearestAirportRequestType.Departure}");
                        if(_nearestAirportRequestType == NearestAirportRequestType.Departure)
                        {
                            ActiveFlight.DepartureAirport = airport;
                        }
                        else if(_nearestAirportRequestType == NearestAirportRequestType.Arrival)
                        {
                            ActiveFlight.ArrivalAirport = airport;
                        }
                    }
                    break;

                case nameof(_simConnectService.IsInFlight):
                    SimConnectInFlight = _simConnectService.IsInFlight;
                    break;
            }
        }

        internal void RequestNearestAirports(NearestAirportRequestType nearestAirportRequestType)
        {
            _nearestAirportRequestType = nearestAirportRequestType;
            _simConnectService.RequestNearestAirport();
        }

        public void Close()
        {
            _simConnectService?.Close();
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
