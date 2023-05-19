using FSTRaK.Models;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace FSTRaK
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
        private Stopwatch _eventsStopwatch = new Stopwatch();
        private int _eventsInterval = 5000;
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

        public struct FlightParams
        {
            public double Heading;
            public double Latitude;
            public double Longitude;
            public double TrueAirspeed;
            public double Altitude;
            public bool IsOnGround;
        }

        public enum FlightState 
        {
            SimNotInFlight,
            Started,
            InTaxi,
            InFlight,
            Landed,
            Parked,
            Ended
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
            _simConnectService.PropertyChanged += SimconnectService_OnPropertyChange;            
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

        private FlightState _state = FlightState.SimNotInFlight;
        public FlightState State { 
            get { return _state; }
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SimconnectService_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {            
            switch(e.PropertyName)
            {
                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;

                    switch (State)
                    {
                        case (FlightState.Started):
                            // Flight started - update the aircraft and departure airport, add first flight event only
                            Aircraft aircraft;
                            aircraft = new Aircraft();
                            aircraft.Title = data.title;
                            aircraft.Type = data.atcType;
                            aircraft.Model = data.model;
                            aircraft.Airline = data.airline;
                            ActiveFlight.Aircraft = aircraft;
                            if(ActiveFlight.FlightEvents.Count == 0)
                            {
                                AddFlightEvent(data);
                            } else if(ActiveFlight.FlightEvents.Count == 1)
                            {
                                if(data.latitude != CurrentFlightParams.Latitude || data.longitude != CurrentFlightParams.Longitude)
                                {
                                    // Airplane in motion, change state
                                    State = FlightState.InTaxi;
                                }
                            }
                            else
                            {
                                throw new Exception("More than one flight event while in Started state!");
                            }

                            break;

                        default:
                            AddFlightEvent(data);
                            break;

                    }

                    // Updating the map in realtime
                    FlightParams fp = new FlightParams();
                    fp.TrueAirspeed = data.trueAirspeed;
                    fp.Heading = data.trueHeading;
                    fp.IsOnGround = data.simOnGround;
                    fp.Latitude = data.latitude;
                    fp.Longitude = data.longitude;
                    fp.Altitude = data.altitude;
                    CurrentFlightParams = fp;
                    OnPropertyChanged("ActiveFlight");

                    break;

                case nameof(SimConnectService.NearestAirport):
                    var airport = _simConnectService.NearestAirport;
                    if(ActiveFlight != null && CurrentFlightParams.IsOnGround)
                    {
                        Log.Debug($"xxx {airport}");
                        ActiveFlight.DepartureAirport = airport;
                    }
                    break;

                case nameof(_simConnectService.IsInFlight):
                    if(_simConnectService.IsInFlight)
                    {
                        StartFlight();
                    } else
                    {
                        EndFlight();
                    }
                    OnPropertyChanged(nameof(SimConnectService.IsInFlight));
                    break;
            }
        }

        private void AddFlightEvent(SimConnectService.AircraftFlightData data)
        {
            DateTime time = CalculateSimTime(data);
            if (ActiveFlight.StartTime == null)
            {
                ActiveFlight.StartTime = time;
            }

            // Saving flight events at intervals
            if (_eventsStopwatch.ElapsedMilliseconds > _eventsInterval || State != FlightState.SimNotInFlight && ActiveFlight.FlightEvents.Count == 0)
            {
                FlightEvent fe = new FlightEvent();
                fe.Altitude = data.altitude;
                fe.GroundAltitude = data.groundAltitude;
                fe.Latitude = data.latitude;
                fe.Longitude = data.longitude;
                fe.TrueHeading = data.trueHeading;
                fe.Airspeed = data.trueAirspeed;
                fe.Time = time;
                ActiveFlight.FlightEvents.Add(fe);
                _eventsStopwatch.Restart();
            }

        }

        private void StartFlight()
        {
            // TODO code to get initial data for flight, create the flight object and start writing events.
            State = FlightState.Started;
            _activeFlight = new Flight();
            _eventsStopwatch.Start();
            Log.Information($"Flight started at {DateTime.Now}");
        }

        private void EndFlight()
        {
            // TODO code to save flight in the db and recycle
            State = FlightState.Ended;

            _eventsStopwatch.Start();
            _eventsStopwatch.Reset();
            Log.Information($"Flight ended at {DateTime.Now}");
        }
        private static DateTime CalculateSimTime(SimConnectService.AircraftFlightData data)
        {
            var day = new DateTime(data.zuluYear, data.zuluMonth, data.zuluDay, 0, 0, 0, 0, System.DateTimeKind.Utc);
            var time = day.AddSeconds(data.zuluTime);
            return time;
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
