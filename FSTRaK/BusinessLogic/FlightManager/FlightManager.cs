using FSTRaK.DataTypes;
using Serilog;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FSTRaK.Models.Entity;
using System.Linq;
using System.Globalization;
using FSTRaK.BusinessLogic.FlightManager.State;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;
using FSTRaK.Models;

namespace FSTRaK.BusinessLogic.FlightManager
{
    /// <summary>
    /// FlightManager is the domain model managing the flight. It's responsibilities are to subscribe to the Simconnect service events, 
    /// manage the flight state, expose the model data to realtime map view and persist the flight when they end.
    /// </summary>
    internal sealed class FlightManager : BaseModel
    {
        private static readonly object Lock = new();
        private static FlightManager _instance = null;
        private FlightManager() { }

        private SimConnectService _simConnectService;
        public static FlightManager Instance
        {
            get
            {
                lock (Lock)
                    return _instance ??= new FlightManager();
            }
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
            _simConnectService.PropertyChanged += SimconnectService_OnPropertyChange;

            // Seed the mirrors from the live service values. Both this side's setters and
            // the service's de-duplicate, so a value that never changes after we subscribe
            // would otherwise never reach us - leaving CameraState at default(CameraState),
            // which is not even a valid member (the enum starts at Cockpit = 2).
            // The backing fields are assigned directly on purpose: going through the
            // properties would raise property changes before State is assigned below, and
            // the change handlers reach State.
            _cameraState = _simConnectService.CameraState;
            _simConnectInFlight = _simConnectService.IsInFlight;
            _simConnectIsConnected = _simConnectService.IsConnected;
            _simVersion = _simConnectService.SimVersion;

            State = new SimNotInFlightState(this);
        }

        // Properties
        private Flight _activeFlight;
        public Flight ActiveFlight
        {
            get => _activeFlight;
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
            get => _currentFlightParams;
            set
            {
                _currentFlightParams = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Aircraft and position from the most recent in-flight sample, used to decide
        /// whether a flight survived a connection gap. Null until a flight has been
        /// sampled, which the identity check reads as "nothing to resume".
        /// </summary>
        public FlightIdentitySnapshot? LastKnownSnapshot { get; private set; }

        private IFlightManagerState _state;
        public IFlightManagerState State { 
            get => _state;
            set
            {
                _state = value;
                Log.Information($"State changed - {value.Name}");
                OnPropertyChanged();
            }
        }

        private bool _simConnectInFlight;
        public bool SimConnectInFlight { 
            get => _simConnectInFlight;
            set
            {
                if( _simConnectInFlight == value ) return; 
                _simConnectInFlight = value; 
                OnPropertyChanged();
            }
        }

        private bool _simConnectIsConnected;
        public bool SimConnectIsConnected
        {
            get => _simConnectIsConnected;
            set
            {
                if (_simConnectIsConnected == value) return;
                _simConnectIsConnected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Mirrors the simulator's camera state. States read it from here rather than from
        /// a FlightData sample: camera state now travels on its own poll, so it is current
        /// even while the SIM_FRAME flight data subscription is silent.
        /// </summary>
        private CameraState _cameraState;
        public CameraState CameraState
        {
            get => _cameraState;
            set
            {
                if (_cameraState == value) return;
                _cameraState = value;
                OnPropertyChanged();
            }
        }

        private string _simVersion;
        public string SimVersion
        {
            get => _simVersion;
            set
            {
                if (_simVersion == value) return;
                _simVersion = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler<int> FlightSaved;

        internal void OnFlightSaved(int flightId)
        {
            FlightSaved?.Invoke(this, flightId);
        }

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

                case nameof(SimConnectService.AircraftData):
                    if(ActiveFlight != null)
                        SetAircraftAsynchronously();
                    break;

                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;
                    State.ProcessFlightData(data);

                    // Updating the map in realtime if not in non-flight states
                    if (State is not SimNotInFlightState)
                    {
                        var fp = new FlightParams
                        {
                            IndicatedAirspeed = data.IndicatedAirspeed,
                            GroundSpeed = data.GroundVelocity,
                            VerticalSpeed = data.VerticalSpeed,
                            Heading = data.TrueHeading,
                            IsOnGround = Convert.ToBoolean(data.SimOnGround),
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            Altitude = data.Altitude
                        };
                        CurrentFlightParams = fp;

                        LastKnownSnapshot = new FlightIdentitySnapshot
                        {
                            Title = ActiveFlight?.Aircraft?.Title,
                            LiveryName = ActiveFlight?.Aircraft?.LiveryName,
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            OnGround = Convert.ToBoolean(data.SimOnGround)
                        };
                    }

                    OnPropertyChanged(nameof(ActiveFlight));
                    break;

                case nameof(SimConnectService.NearestAirport):
                    var airport = _simConnectService.NearestAirport;
                    if(ActiveFlight != null)
                    {
                        switch (_nearestAirportRequestType)
                        {
                            case NearestAirportRequestType.Departure:
                            {
                                ActiveFlight.DepartureAirport = airport;
                                break;
                            }
                            case NearestAirportRequestType.Arrival:
                            case NearestAirportRequestType.CrashedNear:
                            {
                                ActiveFlight.ArrivalAirport = airport;
                                break;
                            }
                            default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                        }
                        var prefix  = (_nearestAirportRequestType == NearestAirportRequestType.Departure) ? "Departing" : "Landed";
                        Log.Information($"{prefix} - found {airport} at {_simConnectService.NearestAirportDistance * Consts.MetersToNauticalMiles} NM");
                    }
                    break;

                case nameof(_simConnectService.IsInFlight):
                    SimConnectInFlight = _simConnectService.IsInFlight;
                    if (!SimConnectInFlight && State is not SimNotInFlightState)
                    {
                        State = new SimNotInFlightState(this);
                    }
                    break;

                case nameof(_simConnectService.IsConnected):
                    SimConnectIsConnected = _simConnectService.IsConnected;
                    break;

                case nameof(_simConnectService.SimVersion):
                    SimVersion = _simConnectService.SimVersion;
                    break;

                case nameof(_simConnectService.CameraState):
                    CameraState = _simConnectService.CameraState;
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Driven by the camera poll rather than the flight-data stream, so that leaving a
        /// flight is still detected while the simulator is paused - which is exactly when
        /// the SIM_FRAME subscription stops delivering. This also closes a case where
        /// pausing after landing and then quitting left a pending touchdown unfinalized.
        /// </summary>
        public void HandleCameraTick()
        {
            State.HandleFlightExitEvent();
        }

        public string GetLoadedAircraftFileName()
        {
            return _simConnectService.LoadedAircraft;
        }

        public void RequestNearestAirports(NearestAirportRequestType nearestAirportRequestType)
        {
            _nearestAirportRequestType = nearestAirportRequestType;
            _simConnectService.RequestNearestAirport();
        }

        public void RequestLoadedAircraft()
        {
            _simConnectService.RequestLoadedAircraft();
        }

        private void SetAircraftAsynchronously()
        {
            _ = Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        var aircraftData = _simConnectService.AircraftData;
                        Aircraft aircraft;
                        // If aircraft is already in the db, let's use the existing record. If no livery name - not in condition
                        if (_simConnectService.SimVersion == SimConnectService.MSFS2020) {
                            aircraft = logbookContext.Aircraft.FirstOrDefault(a => a.Title.Trim() == aircraftData.title.Trim());
                        }
                        else
                        {
                            aircraft = logbookContext.Aircraft.FirstOrDefault(a => a.Title.Trim() == aircraftData.title.Trim() && (a.LiveryName == aircraftData.liveryName));
                        }

                        if (aircraft != null)
                        {
                            aircraft.EmptyWeightLbs ??= aircraftData.EmptyWeightLbs;
                            logbookContext.SaveChanges();
                            ActiveFlight.Aircraft = aircraft;
                        }
                        else
                        {
                            // delete aircraft if it doesn't have empty weight
                            aircraft = logbookContext.Aircraft.Create();
                            aircraft.Title = aircraftData.title.Trim();
                            aircraft.LiveryName = _simConnectService.SimVersion == SimConnectService.MSFS2024 ? aircraftData.liveryName.Trim() : null;
                            aircraft.Manufacturer = aircraftData.atcType.Trim();
                            aircraft.Model = aircraftData.model.Trim();
                            aircraft.AircraftType = aircraftData.model.Trim();
                            aircraft.Airline = aircraftData.airline.Trim();
                            aircraft.TailNumber = aircraftData.AtcId.Trim();
                            aircraft.NumberOfEngines = aircraftData.NumberOfEngines;
                            aircraft.EngineType = aircraftData.EngineType;
                            aircraft.Category = aircraftData.Category;
                            aircraft.EmptyWeightLbs = aircraftData.EmptyWeightLbs;
                            if (_simConnectService.SimVersion == SimConnectService.MSFS2020)
                            {
                                EnrichAircraftDataFromFile(aircraft);
                            }
                            AircraftResolver.ResolveManufacturerAndModel(aircraft);


                            // Capitalize manufacturer name correctly.
                            var cultureInfo = new CultureInfo("en-US");
                            var textInfo = cultureInfo.TextInfo;
                            aircraft.Manufacturer = textInfo.ToTitleCase(aircraft.Manufacturer.ToLower());

                            aircraft = logbookContext.Aircraft.Add(aircraft);
                            ActiveFlight.Aircraft = aircraft;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occurred!");
                    }
                    finally
                    {
                        Log.Information(ActiveFlight.Aircraft.ToString());
                    }
                }

            });
        }

        // PSEUDOCODE / PLAN
        // 1. Guard against null aircraft.
        // 2. Normalize and inspect Manufacturer: if present and appears long/unfriendly, match known substrings
        //    (e.g. "BOEING", "AIRBUS", "CESSNA", "PIPER") and replace with a tidy canonical name.
        // 3. Normalize and inspect AircraftType: if present and appears long/unfriendly, try a list of known
        //    code -> (type, model) mappings and apply the first match.
        // 4. Do not overwrite values when inputs are null/whitespace or no mapping is found.
        // 5. Keep logic simple, readable and easy to extend (mappings arrays).

        private void ResolveManufactorerAndModel(Aircraft aircraft)
        {
            if (aircraft == null) return;

            // Normalize and map manufacturer if it's likely verbose/messy
            if (!string.IsNullOrWhiteSpace(aircraft.Manufacturer) && aircraft.Manufacturer.Length > 10)
            {
                var m = aircraft.Manufacturer.ToUpperInvariant();

                var manufacturerMappings = new (string Key, string Canonical)[]
                {
                    ("BOEING", "Boeing"),
                    ("AIRBUS", "Airbus"),
                    ("CESSNA", "Cessna"),
                    ("PIPER", "Piper")
                };

                foreach (var (key, canonical) in manufacturerMappings)
                {
                    if (m.Contains(key))
                    {
                        aircraft.Manufacturer = canonical;
                        break;
                    }
                }
            }

            // Normalize and map aircraft type -> standardized type and model
            if (!string.IsNullOrWhiteSpace(aircraft.AircraftType) && aircraft.AircraftType.Length > 10)
            {
                var t = aircraft.AircraftType.ToUpperInvariant();

                var typeMappings = new (string Key, string Type, string Model)[]
                {
                    ("B738", "B738", "B737-800"),
                    ("B737", "B737", "B737-700"),
                    ("B739", "B739", "B737-900"),
                    ("B772", "B772", "B777-200ER"),
                    ("B77W", "B77W", "B777-300ER"),
                    ("B77F", "B77F", "B777 Freighter"),
                    ("B77L", "B77L", "B777-300LR"),
                    ("B788", "B788", "B787-800"),
                    ("B789", "B789", "B787-900"),
                    ("B78X", "B78X", "B787-1000"),
                    ("A319", "A319", "A319-200"),
                    ("A320", "A320", "A320-200"),
                    ("A20N", "A20N", "A320 Neo"),
                    ("C172", "C172", "C172"),
                    ("C152", "C152", "C152")
                };

                foreach (var (key, type, model) in typeMappings)
                {
                    if (t.Contains(key))
                    {
                        aircraft.AircraftType = type;
                        aircraft.Model = model;
                        break;
                    }
                }
            }
        }

        private void EnrichAircraftDataFromFile(Aircraft aircraft)
        {
            var filename = GetLoadedAircraftFileName();
            if (String.IsNullOrEmpty(filename))
                return;

            try
            {
                using (var fileStream = File.OpenRead(filename))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 128))
                {
                    String line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        var parts = line.Split('=');
                        if (parts.Length <= 1) continue;
                        if (parts[0].Trim() == "icao_type_designator")
                        {
                            aircraft.AircraftType = parts[1].Trim('"', ' ', '\t');
                        }

                        if (parts[0].Trim() == "icao_manufacturer")
                        {
                            aircraft.Manufacturer = parts[1].Trim('"', ' ', '\t');
                        }

                        if (parts[0].Trim() == "icao_model")
                        {
                            aircraft.Model = parts[1].Trim('"', ' ', '\t');
                        }

                        if (parts[0].Trim() == "atc_id" && parts[1] != null)
                        {
                            var reg = parts[1].Trim('"', ' ', '\t');
                            if(reg.Length > 0)
                            {
                                aircraft.TailNumber = reg;
                            }
                        }
                    }
                }
                Log.Information($"Enriched aircraft data from {filename}");
            }
            catch (Exception ex)
            {
                Log.Error("Could not enrich aircraft from file.", ex);
            }
        }

        public void Close()
        {
            _simConnectService?.Close();
        }

    }
}
