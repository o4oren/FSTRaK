using FSTRaK.Models;
using FSTRaK.Models.FlightManager;
using MapControl;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;

namespace FSTRaK.ViewModels
{
    internal class FlightDataViewModel : BaseViewModel
    {
        FlightManager flightManager = FlightManager.Instance;

        public Flight ActiveFlight
        {
            get
            {
                if (flightManager.ActiveFlight != null)
                    return flightManager.ActiveFlight;
                return null;
            }
        }

        private string _title;
        public string Title
        {
            get { return _title; }
            set 
            { 
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _model;
        public string Model
        {
            get { return _model; }
            set
            {
                _model = value;
                OnPropertyChanged();
            }
        }

        private string _airline;
        public string Airline
        {
            get { return _airline; }
            set
            {
                _airline = value;
                OnPropertyChanged();
            }
        }

        private string _type;
        public string Type
        {
            get { return _type; }
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowAirplane;

        public bool IsShowAirplane
        {
            get { return _isShowAirplane; }
            set { _isShowAirplane = value; OnPropertyChanged(); }
        }

        private bool _isCenterOnAirplane = true;

        public bool IsCenterOnAirplane
        {
            get { return _isCenterOnAirplane; }
            set 
            { 
                if(value != _isCenterOnAirplane) 
                    _isCenterOnAirplane = value; OnPropertyChanged();
            }
        }

        private double _zoomLevel = 13;
        public double ZoomLevel
        {
            get { return _zoomLevel; }
            set
            {
                if (value != _zoomLevel)
                    _zoomLevel = value; 
                OnPropertyChanged();
            }
        }


        public Location Location
        {
            get
            {
                if (flightManager != null && flightManager.ActiveFlight != null)
                    return new Location(flightManager.CurrentFlightParams.Latitude, flightManager.CurrentFlightParams.Longitude);
                return new Location(51,0);
            }
            private set
            { }
        }


        public string FlightParamsText
        {
            get
            {
                if (flightManager != null)
                {
                    return $"Airspeed: {flightManager.CurrentFlightParams.TrueAirspeed:F0} Kts\nAltitude: {flightManager.CurrentFlightParams.Altitude:F0} Ft\nHeading: {flightManager.CurrentFlightParams.Heading:F0} Deg" +
                        $"\nPosition: {flightManager.CurrentFlightParams.Latitude:F4},{flightManager.CurrentFlightParams.Longitude:F4}";
                }
                return "";
            }
        }

        public double Heading
        {
            get
            {
                if (flightManager != null)
                    return flightManager.CurrentFlightParams.Heading;
                return 0;
            }
            private set
            { }
        }

        string _state = "";

        public string State
        {
            get
            {
                return _state;
            }
            private set
            { 
                if(_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Location> FlightPath { get; set; } = new ObservableCollection<Location>();

        private ObservableCollection<Location> _lastSegmentLine;
        public ObservableCollection<Location> LastSegmentLine { get 
            {
                if(_lastSegmentLine != null)
                {
                    return _lastSegmentLine;
                }
                return new ObservableCollection<Location>();
            }
            private set {
                if(_lastSegmentLine != value)
                {
                    _lastSegmentLine = value;
                }
                OnPropertyChanged();
            } 
        }

        public string NearestAirport { get; set; }

        public MapTileLayerBase MapProvider { 
            get 
            {
                string resoueceKey = Properties.Settings.Default.MapTileProvider;
                var resource = Application.Current.Resources[resoueceKey] as MapTileLayerBase;
                if(resource != null)
                {
                    return resource;
                }
                return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
            }
            private set { 
            
            }
        
        }


        private DateTime _lastUpdated = DateTime.Now;

        public FlightDataViewModel()
        {
            flightManager.PropertyChanged += SimconnectManagerUpdate;
        }

        private void SimconnectManagerUpdate(object sender, PropertyChangedEventArgs e)
        {
            
            switch (e.PropertyName)
            {
                case nameof(flightManager.ActiveFlight):
                    // Update flightpath only when starting to move.
                    if (flightManager.State.IsMovementState && _lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(new Location(flightManager.CurrentFlightParams.Latitude, flightManager.CurrentFlightParams.Longitude));
                        _lastUpdated = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(flightManager.ActiveFlight));
                    OnPropertyChanged(nameof(Location));
                    if (FlightPath.Count > 0)
                    {
                        var lastSegment = new ObservableCollection<Location>
                        {
                            FlightPath.Last(),
                            Location
                        };
                        LastSegmentLine = lastSegment;
                    }


                    // Begining of flight
                    if (flightManager.State is FlightStartedState && ActiveFlight != null)
                    {
                        Title = $"Aircraft: {ActiveFlight.Aircraft.Title}";
                        Model = $"Model: {ActiveFlight.Aircraft.Model}";
                        Type = $"Type: {ActiveFlight.Aircraft.Type}";
                        Airline = $"Airline: {ActiveFlight.Aircraft.Airline}";
                    }
                    break;

                // Send property updates for calculated fields
                case nameof(flightManager.CurrentFlightParams):
                    OnPropertyChanged(nameof(Heading));
                    OnPropertyChanged(nameof(Location));
                    OnPropertyChanged(nameof(FlightParamsText));

                    break;

                case nameof(flightManager.State):
                    // View related state change updates
                    IsShowAirplane = flightManager.State is SimNotInFlightState ? false : true;
                    
                    if(flightManager.State is FlightStartedState || flightManager.State is SimNotInFlightState)
                    {
                        FlightPath.Clear();
                        OnPropertyChanged(nameof(FlightPath));
                    }

                    // Set map viewport
                    if (flightManager.State is SimNotInFlightState)
                    {
                        ZoomLevel = 5;
                    } else if (flightManager.State is FlightStartedState)
                    {

                        ZoomLevel = 13.5;
                        IsCenterOnAirplane = true;
                    }

                    State = flightManager.State.Name;

                    break;

                default:
                    break;
            }
        }
    }
}