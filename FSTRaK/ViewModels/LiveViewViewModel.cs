using FSTRaK.Models;
using MapControl;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using FSTRaK.BusinessLogic.FlightManager;
using FSTRaK.BusinessLogic.FlightManager.State;
using FSTRaK.BusinessLogic.VatsimService;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.Utils;
using System.Collections.Generic;
using Serilog;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FSTRaK.ViewModels
{
    internal class LiveViewViewModel : BaseViewModel
    {
        private readonly FlightManager _flightManager = FlightManager.Instance;
        private readonly VatsimService _vatsimService = VatsimService.Instance;


        public RelayCommand CenterOnAirplaneCommand { get; private set; }
        public RelayCommand StopCenterOnAirplaneCommand { get; private set; }
        public RelayCommand EnableVatsimItemCommand { get; private set; }
        public RelayCommand DisableVatsimItemCommand { get; private set; }


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
            get => _isShowAirplane;
            set { _isShowAirplane = value; OnPropertyChanged(); }
        }

        private bool _isCenterOnAirplane = true;

        public bool IsCenterOnAirplane
        {
            get => _isCenterOnAirplane;
            set
            {
                if (value != _isCenterOnAirplane)
                {
                    _isCenterOnAirplane = value; 
                    OnPropertyChanged();
                }
            }
        }

        private bool _isShowVatsimAircraft;
        public bool IsShowVatsimAircraft
        {
            get => _isShowVatsimAircraft;
            set
            {
                if (value != _isShowVatsimAircraft)
                {
                    _isShowVatsimAircraft = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isShowVatsimAirports;
        public bool IsShowVatsimAirports
        {
            get => _isShowVatsimAirports;
            set
            {
                if (value != _isShowVatsimAirports)
                {
                    _isShowVatsimAirports = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isShowVatsimFirs;
        public bool IsShowVatsimFirs
        {
            get => _isShowVatsimFirs;
            set
            {
                if (value != _isShowVatsimFirs)
                {
                    _isShowVatsimFirs = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _airplaneIcon = "";

        public string AirplaneIcon
        {
            get => _airplaneIcon;
            set
            {
                if (value != _airplaneIcon)
                {
                    _airplaneIcon = value; 
                    OnPropertyChanged();
                }
            }
        }

        private double _zoomLevel = 13;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = value;
                OnPropertyChanged();
            }
        }

        private Location _mapCenter = new Location(51, 0);
        public Location MapCenter
        {
            get => _mapCenter;
            set
            {
                if (!_mapCenter.Equals(value))
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
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }

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

        private ObservableCollection<VatsimAicraft> _vatsimAircraftList = new ObservableCollection<VatsimAicraft>();

        public ObservableCollection<VatsimAicraft> VatsimAircraftList
        {
            get => _vatsimAircraftList;
            private set
            {
                if (value != _vatsimAircraftList)
                {
                    _vatsimAircraftList = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, ControlledAirport> ControlledAirports
        {
            get => _vatsimService.ControlledAirports;
            private set
            {

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

        public MapTileLayerBase MapProvider => MapProviderResolver.GetMapProvider();


        private DateTime _lastUpdated = DateTime.Now;

        public LiveViewViewModel()
        {
            _flightManager.PropertyChanged += FlightManagerOnPropertyChanged;
            _vatsimService.PropertyChanged += VatsimServiceOnPropertyChanged;

            CenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = true);
            StopCenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = false);
            EnableVatsimItemCommand = new RelayCommand(o =>
            {
                if (IsShowVatsimAircraft || IsShowVatsimAirports || IsShowVatsimFirs)
                {
                    _vatsimService.Start();
                }
            });
            DisableVatsimItemCommand = new RelayCommand(o =>
            {
                if (!(IsShowVatsimAircraft || IsShowVatsimAirports || _isShowVatsimFirs))
                {
                    _vatsimService.Stop();
                }
            });
        }

        private void VatsimServiceOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(_vatsimService.VatsimData):
                    VatsimData = _vatsimService.VatsimData;  // check if needed after all is done
                    ProcessVatsimData();
                    break;
                case nameof(_vatsimService.ControlledAirports):
                    OnPropertyChanged(nameof(ControlledAirports));
                    break;
                default:
                    break;
            }
        }

        private async void ProcessVatsimData()
        {
            await Task.Run(() =>
            {
                var newVatsimAircraftList = new ObservableCollection<VatsimAicraft>();
                foreach (var pilot in _vatsimData.pilots)
                {
                    var aircraft = new VatsimAicraft(pilot);
                    newVatsimAircraftList.Add(aircraft);
                }



                // Update the UI from the background thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    VatsimAircraftList = newVatsimAircraftList;

                });
            });
        }

        private void FlightManagerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
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


                    if (ActiveFlight?.Aircraft != null)
                    {
                        AirplaneIcon = AircraftResolver.GetAircraftIcon(ActiveFlight.Aircraft).Item1;
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
                    IsShowAirplane = !(_flightManager.State is SimNotInFlightState);

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

        public class VatsimAicraft
        {
            public Pilot Pilot { get;  set; }
            public string IconResource { get;  set; }
            public double ScaleFactror { get;  set; }
            public Location Location { get;  set; }
            public VatsimAicraft(Pilot pilot)
            {
                this.Pilot = pilot;
                (this.IconResource, ScaleFactror ) = pilot.flight_plan != null ? AircraftResolver.GetAircraftIcon(pilot.flight_plan.aircraft_short) : ("B737", 0.75);
                this.Location = new MapControl.Location(pilot.latitude, pilot.longitude);
            }

            public string TooltipText
            {
                get => CreateTooltipText();
                set { }
            }

            private string CreateTooltipText()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{Pilot.callsign} {Pilot.name}");
                if (Pilot.flight_plan != null)
                {
                    sb.AppendLine($"Flying from {Pilot.flight_plan.departure} to {Pilot.flight_plan.arrival}");
                    sb.AppendLine($"{Pilot.flight_plan.aircraft_short}  {Pilot.flight_plan.aircraft}");
                }
                sb.AppendLine($"Altitude: {Pilot.altitude} ft");
                sb.AppendLine($"Heading: {Pilot.heading}");
                sb.AppendLine($"Ground Speed: {Pilot.groundspeed} Kts");

                if (Pilot.flight_plan != null)
                {
                    sb.AppendLine($"Flight Plan:\n {Pilot.flight_plan.route}");
                    sb.AppendLine($"Remarks:\n {Pilot.flight_plan.remarks}");
                }
                return sb.ToString();
            }
        }
    }
}