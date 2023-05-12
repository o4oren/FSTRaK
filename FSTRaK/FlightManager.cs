using FSTRaK.Models;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace FSTRaK
{
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

        string _nearestAirport = string.Empty;
        public string NearestAirport
        {
            get { return _nearestAirport; }
            set
            {
                if(value != _nearestAirport)
                {
                    _nearestAirport = value;
                    OnPropertyChanged();
                }
            }
        }


        public Boolean IsInFlight
        {
            get { return _simConnectService == null ? false : _simConnectService.IsInFlight; }
        }

        private void SimconnectService_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {            
            switch(e.PropertyName)
            {
                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;
                    Aircraft aircraft;
                    if (ActiveFlight.Aircraft == null)
                    {
                        aircraft = new Aircraft();
                        aircraft.Title = data.title;
                        aircraft.Type = data.atcType;
                        aircraft.Model = data.model;
                        aircraft.Airline = data.airline;
                        ActiveFlight.Aircraft = aircraft;
                    }

                    // Updating the map in realtime
                    ActiveFlight.Heading = data.trueHeading;
                    ActiveFlight.Latitude = data.latitude;
                    ActiveFlight.Longitude = data.longitude;

                    DateTime time = CalculateSimTime(data);
                    if (ActiveFlight.StartTime == null)
                    {
                        ActiveFlight.StartTime = time;
                    }

                    // Saving flight events at intervals
                    if (_eventsStopwatch.ElapsedMilliseconds > _eventsInterval)
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
                    
                    OnPropertyChanged("ActiveFlight");
                    break;

                case nameof(SimConnectService.NearestAirport):
                    var airport = _simConnectService.NearestAirport;
                    if(ActiveFlight != null && ActiveFlight.DepartureAirport == null)
                    {
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

        private void StartFlight()
        {
            // TODO code to get initial data for flight, create the flight object and start writing events.
            _activeFlight = new Flight();
            _eventsStopwatch.Start();
            Log.Information($"Flight started at {DateTime.Now}");
        }

        private void EndFlight()
        {
            // TODO code to save flight in the db and recycle
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
