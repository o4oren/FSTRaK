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
using FSTRaK.BusinessLogic.IvaoService;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.Utils;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FSTRaK.DataTypes;
using Serilog;

namespace FSTRaK.ViewModels
{
    internal class LiveViewViewModel : BaseViewModel
    {
        private readonly FlightManager _flightManager = FlightManager.Instance;
        private readonly VatsimService _vatsimService = VatsimService.Instance;
        private readonly IvaoService _ivaoService = IvaoService.Instance;

        // Flight track accumulation
        internal record TrackPoint(double Latitude, double Longitude, int Altitude, DateTime Timestamp);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<TrackPoint>> _pilotTracks = new();
        private readonly object _pilotTracksLock = new object();


        public RelayCommand CenterOnAirplaneCommand { get; private set; }
        public RelayCommand StopCenterOnAirplaneCommand { get; private set; }
        public RelayCommand SelectNetworkCommand { get; private set; }
        public RelayCommand EnableNetworkItemCommand { get; private set; }
        public RelayCommand DisableNetworkItemCommand { get; private set; }


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

        private bool _isVatsimActive;
        public bool IsVatsimActive
        {
            get => _isVatsimActive;
            set
            {
                if (value != _isVatsimActive)
                {
                    _isVatsimActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShowVatsimPilots));
                    OnPropertyChanged(nameof(IsShowVatsimAtc));
                    OnPropertyChanged(nameof(IsAnyNetworkActive));
                }
            }
        }

        private bool _isIvaoActive;
        public bool IsIvaoActive
        {
            get => _isIvaoActive;
            set
            {
                if (value != _isIvaoActive)
                {
                    _isIvaoActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShowIvaoPilots));
                    OnPropertyChanged(nameof(IsShowIvaoAtc));
                    OnPropertyChanged(nameof(IsAnyNetworkActive));
                }
            }
        }

        public bool IsAnyNetworkActive => _isVatsimActive || _isIvaoActive;

        // Per-network toggle state — persisted across network switches
        private bool _vatsimShowPilots = true;
        private bool _vatsimShowAtc = true;
        private bool _ivaoShowPilots = true;
        private bool _ivaoShowAtc = true;

        private bool _isShowPilots;
        public bool IsShowPilots
        {
            get => _isShowPilots;
            set
            {
                if (value != _isShowPilots)
                {
                    _isShowPilots = value;
                    if (_isVatsimActive) IsShowVatsimAircraft = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShowVatsimPilots));
                    OnPropertyChanged(nameof(IsShowIvaoPilots));
                }
            }
        }

        private bool _isShowAtc;
        public bool IsShowAtc
        {
            get => _isShowAtc;
            set
            {
                if (value != _isShowAtc)
                {
                    _isShowAtc = value;
                    if (_isVatsimActive)
                    {
                        IsShowVatsimAirports = value;
                        IsShowVatsimFirs = value;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShowVatsimAtc));
                    OnPropertyChanged(nameof(IsShowIvaoAtc));
                }
            }
        }

        public bool IsShowVatsimPilots => _isVatsimActive && _isShowPilots;
        public bool IsShowVatsimAtc => _isVatsimActive && _isShowAtc;
        public bool IsShowIvaoPilots => _isIvaoActive && _isShowPilots;
        public bool IsShowIvaoAtc => _isIvaoActive && _isShowAtc;

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

        private double _zoomLevel = 5;
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

        string _connectionText;

        public string ConnectionText
        {
            get => _connectionText;
            private set
            {
                if (_connectionText != value)
                {
                    _connectionText = value;
                    OnPropertyChanged();
                }
            }
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

        private BindingList<VatsimAicraft> _vatsimAircraftList = new();

        public BindingList<VatsimAicraft> VatsimAircraftList
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

        private BindingList<VatsimControlledAirport> _vatsimControlledAirports = new();
        public BindingList<VatsimControlledAirport> VatsimControlledAirports
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

        private BindingList<VatsimControlledFir> _vatsimControlledFirs = new();
        public BindingList<VatsimControlledFir> VatsimControlledFirs
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

        private BindingList<VatsimControlledUir> _vatsimControlledUirs = new();
        public BindingList<VatsimControlledUir> VatsimControlledUirs
        {
            get => _vatsimControlledUirs;
            private set
            {
                if (value != _vatsimControlledUirs)
                {
                    _vatsimControlledUirs = value;
                    OnPropertyChanged();
                }
            }
        }

        private BindingList<IvaoAircraft> _ivaoAircraftList = new();
        public BindingList<IvaoAircraft> IvaoAircraftList
        {
            get => _ivaoAircraftList;
            private set
            {
                if (value != _ivaoAircraftList)
                {
                    _ivaoAircraftList = value;
                    OnPropertyChanged();
                }
            }
        }

        private BindingList<IvaoAtcItem> _ivaoAtcList = new();
        public BindingList<IvaoAtcItem> IvaoAtcList
        {
            get => _ivaoAtcList;
            private set
            {
                if (value != _ivaoAtcList)
                {
                    _ivaoAtcList = value;
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

        public string MapAttributionText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                var baseProvider = MapProviderResolver.GetMapProvider();
                if (baseProvider?.Description != null) parts.Add(baseProvider.Description);
                if (Properties.Settings.Default.IsOpenAipEnabled)
                    parts.Add("© [OpenAIP](https://www.openaip.net)");
                var chartProvider = MapProviderResolver.GetChartOverlayProvider();
                if (chartProvider?.Description != null) parts.Add(chartProvider.Description);
                return string.Join(" | ", parts);
            }
        }

        public bool IsMaptillerCMap
        {
            get => MapProvider is MapTilerMapTileLayer;
        }

        public void NotifyMapProviderChanged()
        {
            OnPropertyChanged(nameof(MapProvider));
            OnPropertyChanged(nameof(MapAttributionText));
            OnPropertyChanged(nameof(IsMaptillerCMap));
        }



        private DateTime _lastUpdated = DateTime.Now;

        public LiveViewViewModel()
        {
            _flightManager.PropertyChanged += FlightManagerOnPropertyChanged;
            _vatsimService.PropertyChanged += VatsimServiceOnPropertyChanged;
            _ivaoService.PropertyChanged += IvaoServiceOnPropertyChanged;

            CenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = true);
            StopCenterOnAirplaneCommand = new RelayCommand(o => IsCenterOnAirplane = false);
            SelectNetworkCommand = new RelayCommand(o =>
            {
                var network = (NetworkType)o;

                if (network == NetworkType.Vatsim)
                {
                    if (_isVatsimActive)
                    {
                        // Deactivate VATSIM — save toggle state, stop service, clear collections
                        _vatsimShowPilots = _isShowPilots;
                        _vatsimShowAtc = _isShowAtc;
                        _vatsimService.Stop();
                        IsVatsimActive = false;
                        IsShowVatsimAircraft = false;
                        IsShowVatsimAirports = false;
                        IsShowVatsimFirs = false;
                        VatsimAircraftList.Clear();
                        VatsimControlledAirports.Clear();
                        VatsimControlledFirs.Clear();
                        VatsimControlledUirs.Clear();
                        // Update shared toggles to reflect remaining active network (IVAO) or off
                        if (_isIvaoActive)
                        {
                            IsShowPilots = _ivaoShowPilots;
                            IsShowAtc = _ivaoShowAtc;
                        }
                        else
                        {
                            IsShowPilots = false;
                            IsShowAtc = false;
                        }
                    }
                    else
                    {
                        // Activate VATSIM — restore saved toggle state (default true on first use)
                        IsVatsimActive = true;
                        IsShowPilots = _isIvaoActive ? (_isShowPilots || _vatsimShowPilots) : _vatsimShowPilots;
                        IsShowAtc = _isIvaoActive ? (_isShowAtc || _vatsimShowAtc) : _vatsimShowAtc;
                        if (_isShowPilots || _isShowAtc)
                            _vatsimService.Start();
                    }
                }
                else if (network == NetworkType.Ivao)
                {
                    if (_isIvaoActive)
                    {
                        // Deactivate IVAO — save toggle state, stop service, clear collections
                        _ivaoShowPilots = _isShowPilots;
                        _ivaoShowAtc = _isShowAtc;
                        _ivaoService.Stop();
                        IsIvaoActive = false;
                        IvaoAircraftList.Clear();
                        IvaoAtcList.Clear();
                        // Update shared toggles to reflect remaining active network (VATSIM) or off
                        if (_isVatsimActive)
                        {
                            IsShowPilots = _vatsimShowPilots;
                            IsShowAtc = _vatsimShowAtc;
                        }
                        else
                        {
                            IsShowPilots = false;
                            IsShowAtc = false;
                        }
                    }
                    else
                    {
                        // Activate IVAO — restore saved toggle state (default true on first use)
                        IsIvaoActive = true;
                        IsShowPilots = _isVatsimActive ? (_isShowPilots || _ivaoShowPilots) : _ivaoShowPilots;
                        IsShowAtc = _isVatsimActive ? (_isShowAtc || _ivaoShowAtc) : _ivaoShowAtc;
                        if (_isShowPilots || _isShowAtc)
                            _ivaoService.Start();
                    }
                }
            });

            EnableNetworkItemCommand = new RelayCommand(o =>
            {
                // Called when Pilots or ATC toggle is checked — start any active services that aren't running
                if (_isVatsimActive)
                {
                    if (!IsShowVatsimAircraft) VatsimAircraftList.Clear();
                    if (!IsShowVatsimAirports) VatsimControlledAirports.Clear();
                    if (!IsShowVatsimFirs) { VatsimControlledFirs.Clear(); VatsimControlledUirs.Clear(); }
                    if (_isShowPilots || _isShowAtc)
                    {
                        if (!_vatsimService.Started)
                            _vatsimService.Start();
                        else
                        {
                            if (IsShowVatsimAircraft && _vatsimData != null) ProcessVatsimPilots();
                            if (IsShowVatsimAirports && _vatsimData != null) ProcessVatsimAirports();
                            if (IsShowVatsimFirs && _vatsimData != null) ProcessVatsimCtrFSS();
                        }
                    }
                }
                if (_isIvaoActive)
                {
                    if (!_isShowPilots) ClearIvaoAircraft();
                    if (!_isShowAtc) ClearIvaoAtc();
                    if (_isShowPilots || _isShowAtc)
                    {
                        if (!_ivaoService.Started)
                            _ivaoService.Start();
                        else
                        {
                            if (_isShowPilots && _ivaoService.IvaoData != null) ProcessIvaoPilots();
                            if (_isShowAtc && _ivaoService.IvaoData != null) ProcessIvaoAtc();
                        }
                    }
                }
            });

            DisableNetworkItemCommand = new RelayCommand(o =>
            {
                // Called when Pilots or ATC toggle is unchecked — stop services if nothing left to show
                if (_isVatsimActive && !_isShowPilots && !_isShowAtc)
                    _vatsimService.Stop();
                if (_isIvaoActive && !_isShowPilots && !_isShowAtc)
                    _ivaoService.Stop();
            });
        }

        private void VatsimServiceOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(_vatsimService.VatsimData):
                    VatsimData = _vatsimService.VatsimData;
                    if (VatsimData == null)
                    {
                        VatsimAircraftList.Clear();
                        VatsimControlledAirports.Clear();
                        VatsimControlledFirs.Clear();
                        VatsimControlledUirs.Clear();
                        break;
                    }
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

        private void IvaoServiceOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IvaoService.IvaoData):
                    if (IsShowPilots)
                        ProcessIvaoPilots();
                    else
                        IvaoAircraftList.Clear();

                    if (IsShowAtc)
                        ProcessIvaoAtc();
                    else
                        IvaoAtcList.Clear();
                    break;
            }
        }

        private async void ProcessIvaoPilots()
        {
            var data = _ivaoService.IvaoData;
            if (data?.pilots == null) return;
            var myId = Properties.Settings.Default.IvaoId?.Trim();
            var newList = new System.Collections.Generic.List<IvaoAircraft>();
            await Task.Run(() =>
            {
                foreach (var pilot in data.pilots)
                {
                    if (!string.IsNullOrEmpty(myId) && pilot.userId.ToString() == myId) continue;
                    if (pilot.lastTrack == null) continue;
                    newList.Add(new IvaoAircraft(pilot));
                    var key = $"IVAO:{pilot.callsign}";
                    var list = _pilotTracks.GetOrAdd(key, _ => new List<TrackPoint>());
                    lock (list)
                    {
                        list.Add(new TrackPoint(pilot.lastTrack.latitude, pilot.lastTrack.longitude, pilot.lastTrack.altitude, DateTime.UtcNow));
                        if (list.Count > 500)
                            list.RemoveAt(0);
                    }
                }
            });
            IvaoAircraftList.ReplaceContent(newList);
            var activeIvaoKeys = newList.Select(a => $"IVAO:{a.Callsign}").ToHashSet();
            foreach (var k in _pilotTracks.Keys.Where(k => k.StartsWith("IVAO:") && !activeIvaoKeys.Contains(k)).ToList())
                _pilotTracks.TryRemove(k, out _);
        }

        private async void ProcessIvaoAtc()
        {
            var data = _ivaoService.IvaoData;
            if (data?.atcEntries == null) return;
            var newList = new System.Collections.Generic.List<IvaoAtcItem>();
            await Task.Run(() =>
            {
                // Group airport-type entries by airportId, like VATSIM groups by ICAO
                var airportDict = new Dictionary<string, System.Collections.Generic.List<IvaoAtcEntry>>();
                foreach (var atc in data.atcEntries)
                {
                    if (atc.atcPosition?.airport != null)
                    {
                        var icao = atc.atcPosition.airportId;
                        if (!airportDict.TryGetValue(icao, out var list))
                        {
                            list = new System.Collections.Generic.List<IvaoAtcEntry>();
                            airportDict[icao] = list;
                        }
                        list.Add(atc);
                    }
                    else if (atc.subcenter != null)
                    {
                        // CTR entries — one item per entry
                        newList.Add(new IvaoAtcItem(atc));
                    }
                }

                foreach (var group in airportDict.Values)
                    newList.Add(new IvaoAtcItem(group));
            });
            IvaoAtcList.ReplaceContent(newList);
        }

        private void ClearIvaoAircraft() => IvaoAircraftList.Clear();
        private void ClearIvaoAtc() => IvaoAtcList.Clear();

        private async void ProcessVatsimAirports()
        {
            var controlledAirportsDict = new Dictionary<string, VatsimControlledAirport>();
            await Task.Run(() =>
            {
                foreach (var controller in VatsimData.controllers)
                {
                    if (controller.callsign.Equals("DEN_I_APP"))
                    {

                    }

                    if (controller.facility == 2 || controller.facility == 3 || controller.facility == 4 || controller.facility == 5)
                    {
                        // Find airport
                        var callsignParts = controller.callsign.Split('_');
                        var airport = _vatsimService.VatsimStaticData.Airports.Find(a => a.ICAO.Equals(callsignParts[0]) || a.IATA.Equals(callsignParts[0]));
                        if (airport != null && controlledAirportsDict.TryGetValue(airport.ICAO, out var controlledAirport1))
                        {
                            controlledAirport1.Controllers.Add(controller);
                        }
                        else
                        {
                            if (airport == null) continue;
                            var controlledAirport = new VatsimControlledAirport(airport);
                            controlledAirport.Controllers.Add(controller);
                            controlledAirportsDict.Add(controlledAirport.Airport.ICAO, controlledAirport);
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

                    StringUtil.RemoveTrailingWhitespace(sb);
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

                    // create approach area: use TRACON polygon if available, else fall back to 80km circle
                    if (isIncludeApp)
                    {
                        var appController = airport.Controllers.First(c => c.facility == 5);
                        var traconPrefix = appController.callsign.Split('_')[0];

                        // The TRACON GeoJSON "suffix" field is a geographic qualifier (e.g. "N", "S"),
                        // not the VATSIM facility type ("APP"/"DEP"). Look up by prefix only;
                        // GetTraconPolygons will check suffix-specific entries when the GeoJSON has them.
                        var traconPolygons = _vatsimService.GetTraconPolygons(traconPrefix, null);
                        if (traconPolygons.Count > 0)
                        {
                            airport.TraconPolygons = traconPolygons;
                            airport.IsShowCircle = false;
                        }
                        else
                        {
                            // Fallback: 80km circle
                            int numberOfVertices = 80;
                            double radius = 80;
                            airport.IsShowCircle = true;
                            airport.CircleLocations = new LocationCollection();
                            for (int i = 0; i < numberOfVertices; i++)
                            {
                                double angle = (i * 2 * Math.PI) / numberOfVertices;
                                double latitude = airport.Airport.Latitude + (radius / 111.32) * Math.Sin(angle);
                                double longitude = airport.Airport.Longitude + (radius / (111.32 * Math.Cos(47.6097 * (Math.PI / 180)))) * Math.Cos(angle);
                                airport.CircleLocations.Add(new Location(latitude, longitude));
                            }
                        }
                    }
                }

            });
            VatsimControlledAirports.ReplaceContent(controlledAirportsDict.Values.ToList());
        }

        private async void ProcessVatsimPilots()
        {
            var newVatsimAircraftList = new List<VatsimAicraft>();
            await Task.Run(() =>
            {
                foreach (var pilot in _vatsimData.pilots)
                {
                    var aircraft = new VatsimAicraft(pilot);
                    newVatsimAircraftList.Add(aircraft);
                    var key = $"VATSIM:{pilot.callsign}";
                    var list = _pilotTracks.GetOrAdd(key, _ => new List<TrackPoint>());
                    lock (list)
                    {
                        list.Add(new TrackPoint(pilot.latitude, pilot.longitude, pilot.altitude, DateTime.UtcNow));
                        if (list.Count > 500)
                            list.RemoveAt(0);
                    }
                }
            });
            VatsimAircraftList.ReplaceContent(newVatsimAircraftList);
            // Remove tracks for pilots no longer in feed
            var activeVatsimKeys = newVatsimAircraftList.Select(a => $"VATSIM:{a.Pilot.callsign}").ToHashSet();
            foreach (var k in _pilotTracks.Keys.Where(k => k.StartsWith("VATSIM:") && !activeVatsimKeys.Contains(k)).ToList())
                _pilotTracks.TryRemove(k, out _);
        }

        private async void ProcessVatsimCtrFSS()
        {
            var firsList = new List<VatsimControlledFir>();
            var uirDict = new Dictionary<string, VatsimControlledUir>();

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
                            List<LocationCollection> locations = new List<LocationCollection>();
                            foreach (var firMetadataTuple in firs)
                            {
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

                                VatsimControlledUir controlledUir = null;
                                if (firs.Count > 1)
                                {
                                    var uir = _vatsimService.VatsimStaticData.UIRs.FirstOrDefault(u =>
                                        u.CallsignPrefix.Equals(controller.callsign.Split('_')[0]));
                                    uirDict.TryGetValue(uir.CallsignPrefix, out controlledUir);
                                    if (controlledUir == null)
                                    {
                                        controlledUir = new VatsimControlledUir()
                                        {
                                            Name = uir.Name,
                                            Callsign = uir.CallsignPrefix,
                                            FirLocations = locations
                                        };
                                        uirDict.Add(uir.CallsignPrefix, controlledUir);
                                    }

                                    controlledUir.Controllers.Add(controller);
                                }
                                else
                                {

                                    VatsimControlledFir vatsimControlledFir = null;
                                    foreach (var controlledFir in firsList)
                                    {
                                        if (controlledFir.LabelLocation.Equals(firMetadataTuple.labelCoordinates))
                                        {
                                            vatsimControlledFir = controlledFir;
                                        }
                                    }
                                    if (vatsimControlledFir == null)
                                    {
                                        vatsimControlledFir = new VatsimControlledFir();
                                        vatsimControlledFir.LabelLocation = firMetadataTuple.labelCoordinates;
                                        vatsimControlledFir.Locations = locations;
                                        vatsimControlledFir.Name = firMetadataTuple.firName;
                                        firsList.Add(vatsimControlledFir);
                                    }

                                    vatsimControlledFir.Controllers.Add(controller);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, ex.Message);
                        }
                    }
                }
            });

            VatsimControlledFirs.ReplaceContent(firsList);
            VatsimControlledUirs.ReplaceContent(uirDict.Values.ToList());
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
                    else if (_flightManager.State is FlightStartedState { IsStarted: true })
                    {
                        ZoomLevel = 13;
                        IsCenterOnAirplane = true;
                    }

                    State = _flightManager.State.Name;

                    break;

                case nameof(_flightManager.SimVersion):
                case nameof(_flightManager.SimConnectIsConnected):
                    ConnectionText = $"{(_flightManager.SimConnectIsConnected ? "Connected to " : "Not connected to sim")} {(_flightManager.SimVersion != null ? _flightManager.SimVersion : "")}";
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
                StringUtil.RemoveTrailingWhitespace(sb);
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
            public List<LocationCollection> TraconPolygons { get; set; }
            public bool IsShowTraconPolygon => TraconPolygons != null && TraconPolygons.Count > 0;
            public string Callsign => Airport?.ICAO ?? "";

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
                    StringUtil.RemoveTrailingWhitespace(sb);
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
                    StringUtil.RemoveTrailingWhitespace(sb);
                    return sb.ToString();
                }
                private set { }
            }
        }

        public class VatsimControlledUir
        {
            public List<LocationCollection> FirLocations { get; set; } = new List<LocationCollection>();
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
                    StringUtil.RemoveTrailingWhitespace(sb);
                    return sb.ToString();
                }
                private set { }
            }
            public string Callsign { get; set; }
            public string Name { get; set; }
            public string Label
            {
                get
                {
                    return Name;
                }
                private set { }
            }
            public Location LabelLocation
            {
                get
                {
                    return CoordinatesUtil.CalculateCenter(FirLocations);
                }
                private set { }
            }
        }

        internal class IvaoAircraft
        {
            public IvaoPilot Pilot { get; }
            public string Callsign { get; }
            public Location Location { get; set; }
            public double Heading { get; set; }
            public string Icon { get; set; }
            public string TooltipText { get; set; }

            public IvaoAircraft(IvaoPilot pilot)
            {
                Pilot = pilot;
                Callsign = pilot.callsign;
                Location = new Location(pilot.lastTrack.latitude, pilot.lastTrack.longitude);
                Heading = pilot.lastTrack.heading;
                Icon = AircraftResolver.GetAircraftIcon(pilot.flightPlan?.aircraftId ?? "").Item1;

                var departure = pilot.flightPlan?.departureId ?? "";
                var destination = pilot.flightPlan?.arrivalId ?? "";
                var aircraft = pilot.flightPlan?.aircraftId ?? "";
                TooltipText = $"{pilot.callsign}\n{departure} → {destination}\n{aircraft}\nALT: {pilot.lastTrack.altitude}  GS: {pilot.lastTrack.groundSpeed}";
            }
        }

        internal class IvaoAtcItem
        {
            public Location Location { get; set; }
            public string IconResourse { get; set; }
            public string TooltipText { get; set; }
            public LocationCollection ControlPolygon { get; set; }
            public bool IsCtr { get; set; }
            public string Callsign { get; private set; }
            public List<IvaoAtcEntry> AtcEntries { get; private set; }
            public IvaoAtcEntry SingleEntry { get; private set; }

            // Constructor for grouped airport entries (TWR, GND, APP, DEP at the same airport)
            public IvaoAtcItem(System.Collections.Generic.List<IvaoAtcEntry> entries)
            {
                Callsign = entries[0].atcPosition.airportId;
                AtcEntries = entries;
                var first = entries[0];
                Location = new Location(first.atcPosition.airport.latitude, first.atcPosition.airport.longitude);

                var positions = new HashSet<string>(entries.Select(e => e.atcSession?.position ?? ""));
                bool hasApp = positions.Contains("APP") || positions.Contains("DEP");
                bool hasTwr = positions.Contains("TWR");
                bool hasGnd = positions.Contains("GND");

                // Same logic as VATSIM: APP/DEP = radar, TWR = tower, GND = radio
                if (hasApp)
                    IconResourse = hasTwr ? Consts.TowerRadarImage : hasGnd ? Consts.RadioRadarImage : Consts.RadarImage;
                else if (hasTwr)
                    IconResourse = Consts.TowerImage;
                else
                    IconResourse = Consts.RadioImage;

                // Tooltip: airport name + all controllers
                var sb = new StringBuilder();
                sb.AppendLine($"{first.atcPosition.airportId} {first.atcPosition.atcCallsign}");
                sb.AppendLine();
                foreach (var e in entries)
                    sb.AppendLine($"{e.callsign} {e.atcSession?.frequency.ToString("F3")}");
                StringUtil.RemoveTrailingWhitespace(sb);
                TooltipText = sb.ToString();

                // Use APP polygon if available, otherwise first entry with a polygon
                var appEntry = entries.FirstOrDefault(e => (e.atcSession?.position == "APP" || e.atcSession?.position == "DEP") && e.atcPosition.regionMap?.Count > 0);
                var polyEntry = appEntry ?? entries.FirstOrDefault(e => e.atcPosition.regionMap?.Count > 0);
                if (polyEntry != null)
                {
                    ControlPolygon = new LocationCollection();
                    foreach (var pt in polyEntry.atcPosition.regionMap)
                        ControlPolygon.Add(new Location(pt.lat, pt.lng));
                }
            }

            // Constructor for CTR/subcenter entries
            public IvaoAtcItem(IvaoAtcEntry entry)
            {
                Callsign = entry.callsign;
                SingleEntry = entry;
                IsCtr = true;
                Location = new Location(entry.subcenter.latitude, entry.subcenter.longitude);
                IconResourse = Consts.RadarImage;
                var freq = entry.atcSession?.frequency.ToString("F3") ?? "";
                TooltipText = $"{entry.callsign}\n{entry.subcenter.atcCallsign}\n{freq}";
                if (entry.subcenter.regionMap?.Count > 0)
                {
                    ControlPolygon = new LocationCollection();
                    foreach (var pt in entry.subcenter.regionMap)
                        ControlPolygon.Add(new Location(pt.lat, pt.lng));
                }
            }
        }
    }
}