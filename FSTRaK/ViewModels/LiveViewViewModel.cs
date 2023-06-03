using FSTRaK.Models;
using FSTRaK.Models.FlightManager;
using MapControl;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace FSTRaK.ViewModels
{
    internal class LiveViewViewModel : BaseViewModel
    {
        private FlightManager _flightManager = FlightManager.Instance;

        public RelayCommand CenterOnAirplaneCommand { get; private set; }
        public RelayCommand StopCenterOnAirplaneCommand { get; private set; }

        public Flight ActiveFlight
        {
            get
            {
                if (_flightManager.ActiveFlight != null)
                    return _flightManager.ActiveFlight;
                return null;
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
                if (value != _isCenterOnAirplane)
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

        private Location _mapCenter = new Location(51, 0);
        public Location MapCenter
        {
            get { return _mapCenter; }
            set
            {
                if (_mapCenter != value)
                {
                    _mapCenter = value;
                    OnPropertyChanged();
                }
            }
        }


        public Location Location
        {
            get
            {
                if (_flightManager != null && _flightManager.ActiveFlight != null)
                    return new Location(_flightManager.CurrentFlightParams.Latitude, _flightManager.CurrentFlightParams.Longitude);
                return new Location(51, 0);
            }
            private set { }
        }


        public string FlightParamsText
        {
            get
            {
                if (_flightManager != null)
                {
                    return $"Airspeed: {_flightManager.CurrentFlightParams.IndicatedAirspeed:F0} Kts\n" +
                        $"Ground speed: { _flightManager.CurrentFlightParams.GroundSpeed:F0} Kts\n" +
                        $"Altitude: {_flightManager.CurrentFlightParams.Altitude:F0} Ft\n" +
                        $"Heading: {_flightManager.CurrentFlightParams.Heading:F0} Deg" +
                        $"\nPosition: {_flightManager.CurrentFlightParams.Latitude:F4},{_flightManager.CurrentFlightParams.Longitude:F4}";
                }
                return "";
            }
        }

        public double Heading
        {
            get
            {
                if (_flightManager != null)
                    return _flightManager.CurrentFlightParams.Heading;
                return 0;
            }
            private set { }
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
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Location> FlightPath { get; set; } = new ObservableCollection<Location>();

        private ObservableCollection<Location> _lastSegmentLine;
        public ObservableCollection<Location> LastSegmentLine
        {
            get
            {
                if (_lastSegmentLine != null)
                {
                    return _lastSegmentLine;
                }
                return new ObservableCollection<Location>();
            }
            private set
            {
                if (_lastSegmentLine != value)
                {
                    _lastSegmentLine = value;
                }
                OnPropertyChanged();
            }
        }

        public string NearestAirport { get; set; }

        public MapTileLayerBase MapProvider
        {
            get
            {
                string resoueceKey = Properties.Settings.Default.MapTileProvider;
                var resource = Application.Current.Resources[resoueceKey] as MapTileLayerBase;
                if (resource != null)
                {
                    return resource;
                }
                return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
            }
            private set{}
        }


        private DateTime _lastUpdated = DateTime.Now;

        public LiveViewViewModel()
        {
            _flightManager.PropertyChanged += SimconnectManagerUpdate;
            CenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = true);
            StopCenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = false);
        }

        private void SimconnectManagerUpdate(object sender, PropertyChangedEventArgs e)
        {

            switch (e.PropertyName)
            {
                case nameof(_flightManager.ActiveFlight):
                    // Update flightpath only when starting to move.
                    if (_flightManager.State.IsMovementState && _lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(new Location(_flightManager.CurrentFlightParams.Latitude, _flightManager.CurrentFlightParams.Longitude));
                        _lastUpdated = DateTime.Now;
                    }

                    if (IsCenterOnAirplane)
                    {
                        MapCenter = Location;
                    }

                    if (FlightPath.Count > 0 && _flightManager.State.IsMovementState)
                    {
                        var lastSegment = new ObservableCollection<Location>
                        {
                            FlightPath.Last(),
                            Location
                        };
                        LastSegmentLine = lastSegment;
                    }


                    OnPropertyChanged(nameof(_flightManager.ActiveFlight));
                    OnPropertyChanged(nameof(Location));
                    break;

                // Send property updates for calculated fields
                case nameof(_flightManager.CurrentFlightParams):
                    OnPropertyChanged(nameof(Heading));
                    OnPropertyChanged(nameof(Location));
                    OnPropertyChanged(nameof(FlightParamsText));

                    break;

                case nameof(_flightManager.State):
                    // View related state change updates
                    IsShowAirplane = _flightManager.State is SimNotInFlightState ? false : true;

                    if (_flightManager.State is FlightStartedState || _flightManager.State is SimNotInFlightState)
                    {
                        FlightPath.Clear();
                        OnPropertyChanged(nameof(FlightPath));
                    }

                    // Set map viewport
                    if (_flightManager.State is SimNotInFlightState)
                    {
                        ZoomLevel = 5;
                    }
                    else if (_flightManager.State is FlightStartedState)
                    {

                        ZoomLevel = 13;
                        IsCenterOnAirplane = true;
                        
                    }

                    State = _flightManager.State.Name;

                    break;

                default:
                    break;
            }
        }
    }
}