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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using FSTRaK.DataTypes;
using System.Windows.Controls;
using Serilog;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

        private Location _mapCenter = new(51, 0);
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

        private ObservableCollection<VatsimAicraft> _vatsimAircraftList = new();

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

        private ObservableCollection<VatsimControlledAirport> _vatsimControlledAirports = new();
        public ObservableCollection<VatsimControlledAirport> VatsimControlledAirports
        {
            get => _vatsimControlledAirports;
            private set
            {
                if (value != _vatsimControlledAirports)
                {
                    _vatsimControlledAirports = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<VatsimControlledFir> _vatsimControlledFirs = new();
        public ObservableCollection<VatsimControlledFir> VatsimControlledFirs
        {
            get => _vatsimControlledFirs;
            private set
            {
                if (value != _vatsimControlledFirs)
                {
                    _vatsimControlledFirs = value;
                    OnPropertyChanged();
                }
            }
        }

        

        public ObservableCollection<Location> FlightPath { get; set; } = new();

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
                    if (IsShowVatsimAircraft)
                    {
                        ProcessVatsimPilots();
                    }
                    else
                    {
                        VatsimAircraftList.Clear();
                    }

                    if (IsShowVatsimAirports)
                    {
                        ProcessVatsimAirports();
                    }
                    else
                    {
                        VatsimControlledAirports.Clear();
                    }

                    if (IsShowVatsimFirs)
                    {
                        ProcessVatsimCtrFSS();
                    }
                    else
                    {
                        VatsimControlledFirs.Clear();
                    }
                    break;
                default:
                    break;
            }
        }

        private async void ProcessVatsimAirports()
        {
            var airportsList = new List<VatsimControlledAirport>();
            await Task.Run(() =>
            {
                var controlledAirportsDict = new Dictionary<string, VatsimControlledAirport>();
                foreach (var controller in VatsimData.controllers)
                {
                    if (controller.facility == 2 || controller.facility == 3 || controller.facility == 4 || controller.facility == 5)
                    {
                        // Find airport
                        var callsignParts = controller.callsign.Split('_');
                        if (controlledAirportsDict.ContainsKey(callsignParts[0]))
                        {
                            var airport = controlledAirportsDict[callsignParts[0]];
                            airport.Controllers.Add(controller);
                        }
                        else
                        {
                            var airport = _vatsimService.VatsimStaticData.Airports.Find(a => a.ICAO.Equals(callsignParts[0]));
                            if (airport != null)
                            {
                                var controlledAirport = new VatsimControlledAirport(airport);
                                controlledAirport.Controllers.Add(controller);
                                controlledAirportsDict.Add(controlledAirport.Airport.ICAO, controlledAirport);
                            }
                        }
                    }
                }

                foreach (var atis in VatsimData.atis)
                {

                    var callsignParts = atis.callsign.Split('_');
                    if (controlledAirportsDict.ContainsKey(callsignParts[0]))
                    {
                        var airport = controlledAirportsDict[callsignParts[0]];
                        airport.Atis.Add(atis);
                    }
                    else
                    {
                        var airport = _vatsimService.VatsimStaticData.Airports.Find(a => a.ICAO.Equals(callsignParts[0]));
                        if (airport != null)
                        {
                            var controlledAirport = new VatsimControlledAirport(airport);
                            controlledAirport.Atis.Add(atis);
                            controlledAirportsDict.Add(controlledAirport.Airport.ICAO, controlledAirport);
                        }
                    }
                }

                foreach (var airport in controlledAirportsDict.Values)
                {
                    var facilities = new HashSet<int>();
                    StringBuilder sb = new StringBuilder();
                    bool isIncludeApp = false;

                    sb.AppendLine($"{airport.Airport.ICAO} {airport.Airport.Name}");
                    sb.AppendLine();
                    sb.AppendLine("Controllers:");

                    foreach (var controller in airport.Controllers)
                    {
                        facilities.Add(controller.facility);

                        sb.AppendLine($"{controller.callsign} {controller.name} {controller.frequency} Connected for: {TimeUtils.GetConnectionsSinceFromTimeString(controller.logon_time)}");
                        if (controller.facility == 5)
                        {
                            isIncludeApp = true;
                        }
                    }

                    foreach (var atis in airport.Atis)
                    {
                        if (atis.text_atis != null)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"{atis.callsign} {atis.name} {atis.frequency}:");
                            foreach (var message in atis.text_atis)
                            {
                                sb.AppendLine(message);
                            }
                        }
                    }

                    airport.TooltipText = sb.ToString();
                    
                    if (facilities.Contains(5))
                    {
                        if (facilities.Contains(3) || facilities.Contains(4))
                        {
                            airport.IconResourse = Consts.TowerRadarImage;
                        }
                        else if (facilities.Contains(2) || airport.Atis.Count > 0)
                        {
                            airport.IconResourse = Consts.RadioRadarImage;
                        }
                        else
                        {
                            airport.IconResourse = Consts.RadarImage;
                        }
                    }
                    else
                    {
                        if (facilities.Contains(3) || facilities.Contains(4))
                        {
                            airport.IconResourse = Consts.TowerImage;
                        }
                        else if (facilities.Contains(2) || airport.Atis.Count > 0)
                        {
                            airport.IconResourse = Consts.RadioImage;
                        }
                    }

                    // create approach circle locations
                    if (isIncludeApp)
                    {
                        int numberOfVertices = 80; // Adjust as needed for smoothness
                        double radius = 80;
                        airport.IsShowCircle = true;
                        airport.CircleLocations = new LocationCollection();
                        for (int i = 0; i < numberOfVertices; i++)
                        {
                            double angle = (i * 2 * Math.PI) / numberOfVertices;
                            double latitude = airport.Airport.Latitude + (radius / 111.32) * Math.Sin(angle); // 1 degree of latitude is approximately 111.32 km
                            double longitude = airport.Airport.Longitude + (radius / (111.32 * Math.Cos(47.6097 * (Math.PI / 180)))) * Math.Cos(angle);
                            airport.CircleLocations.Add(new Location(latitude, longitude));
                        }
                    }
                }

                airportsList.AddRange(controlledAirportsDict.Values.ToList());
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    VatsimControlledAirports = new ObservableCollection<VatsimControlledAirport>(airportsList);
                });
            });

            VatsimControlledAirports = new ObservableCollection<VatsimControlledAirport>(airportsList);
        }

        private async void ProcessVatsimPilots()
        {
            var newVatsimAircraftList = new ObservableCollection<VatsimAicraft>();
            await Task.Run(() =>
            {
                foreach (var pilot in _vatsimData.pilots)
                {
                    var aircraft = new VatsimAicraft(pilot);
                    newVatsimAircraftList.Add(aircraft);
                }
            });

            VatsimAircraftList = newVatsimAircraftList;
        }

        private async void ProcessVatsimCtrFSS()
        {
            var firsList = new List<VatsimControlledFir>();

            await Task.Run(() =>
            {
                foreach (var controller in VatsimData.controllers)
                {
                    if (controller.facility == 6 || controller.facility == 1)
                    {
                        try
                        {

                            if (controller.frequency.Equals("199.998"))
                            {
                                continue;
                            }

                            // TODO review this logic
                            // For UIRs and FIRs crossing the dateline
                            var firs = _vatsimService.GetBoundariesArrayByController(controller);

                            if (firs.Count == 0)
                            {
                                // For most FIRs
                                try
                                {
                                    var firMetadataTuple =
                                        VatsimService.Instance.GetFirBoundariesByController(controller);
                                    firs.Add(firMetadataTuple);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.Message, ex);
                                    continue;
                                }
                            }

                            // IF fIRS > 0 this is a UIR TODO handle UIR in the same or another type
                            foreach (var firMetadataTuple in firs)
                            {

                                List<LocationCollection> locations = new List<LocationCollection>();

                                
                                foreach (var geoJsonCoordinate in firMetadataTuple.coordinates)
                                {
                                    {
                                        LocationCollection locationCollection = new LocationCollection();
                                        foreach (var coords in geoJsonCoordinate[0])
                                        {
                                            locationCollection.Add(new Location(coords[1], coords[0]));
                                        }
                                        locations.Add(locationCollection);
                                    }
                                }


                                VatsimControlledFir vatsimControlledFir = null;
                                foreach (var controlledFir in firsList)
                                {
                                    if (controlledFir.LabelLocation.Equals(new Location(firMetadataTuple.labelCoordinates[0], firMetadataTuple.labelCoordinates[1])))
                                    {
                                        vatsimControlledFir = controlledFir;
                                    }
                                }

                                if (vatsimControlledFir == null)
                                {
                                    vatsimControlledFir = new VatsimControlledFir();
                                    vatsimControlledFir.LabelLocation = new Location(firMetadataTuple.labelCoordinates[0], firMetadataTuple.labelCoordinates[1]);
                                    vatsimControlledFir.Locations = locations;
                                    vatsimControlledFir.Name = firMetadataTuple.firName;
                                    firsList.Add(vatsimControlledFir);
                                }

                                vatsimControlledFir.Controllers.Add(controller);
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, ex.Message);
                        }
                    }
                }
            });
            VatsimControlledFirs = new ObservableCollection<VatsimControlledFir>(firsList);

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
            public Pilot Pilot { get; set; }
            public string IconResource { get; set; }
            public double ScaleFactror { get; set; }
            public Location Location { get; set; }
            public VatsimAicraft(Pilot pilot)
            {
                this.Pilot = pilot;
                (this.IconResource, ScaleFactror) = pilot.flight_plan != null ? AircraftResolver.GetAircraftIcon(pilot.flight_plan.aircraft_short) : ("B737", 0.75);
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

        public class VatsimControlledAirport
        {
            public VatsimStaticData.Airport Airport { get; private set; }
            public List<Controller> Controllers { get; private set; }
            public List<Atis> Atis { get; private set; }
            public string IconResourse { get; set; }
            public string TooltipText { get; set; }
            public bool IsShowCircle { get; set; } = false;

            public Location Location
            {
                get => new(Airport.Latitude, Airport.Longitude);
                set { }
            }

            public LocationCollection CircleLocations { get; set; }
            public VatsimControlledAirport(VatsimStaticData.Airport airport)
            {
                this.Airport = airport;
                Controllers = new List<Controller>();
                Atis = new List<Atis>();
            }
        }

        public class VatsimControlledFir
        {
            public HashSet<Controller> Controllers { get; private set; } = new();
            public string TooltipText
            {
                get
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(Name);
                    sb.AppendLine();
                    foreach (var controller in Controllers)
                    {
                        sb.AppendLine($"{controller.callsign} {controller.name} {controller.frequency} Connected for: {TimeUtils.GetConnectionsSinceFromTimeString(controller.logon_time)}");
                    }
                    return sb.ToString();
                }
                private set { }
            }

            public List<LocationCollection> Locations { get; set; }
            public string Name { get; set; }

            public Location LabelLocation { get; set; }

            public string Label
            {
                get
                {
                    var sb = new StringBuilder();
                    foreach (var controller in Controllers)
                    {
                        sb.AppendLine(controller.callsign.Replace("_", "__"));
                    }
                    if(char.IsWhiteSpace(sb[sb.Length -1]))
                    {
                        sb.Remove(sb.Length - 1, 1);
                    }
                    return sb.ToString();
                }
                private set { }
            }
        }

        public class VatsimControlledUir
        {
            public List<Controller> Controllers { get; private set; }
            public string TooltipText { get; set; }
            public PolygonCollection Polygons { get; private set; }
            public LocationCollection Location { get; set; }
            public Location LabelLocation { get; set; }
        }
    }
}