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
                    State.HandleFlightExitEvent();

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

                default:
                    break;
            }
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
                            ResolveManufactorerAndModel(aircraft);

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
                    ("PIPER", "Piper"),
                    ("EMBRAER", "Embraer"),
                    ("BOMBARDIER", "Bombardier"),
                    ("ATR", "ATR"),
                    ("DAHER", "Daher"),
                    ("CIRRUS", "Cirrus"),
                    ("BEECHCRAFT", "Beechcraft"),
                    ("PILATUS", "Pilatus"),
                    ("FOKKER", "Fokker"),
                    ("LEARJET", "Learjet"),
                    ("HONDA", "Honda"),
                    ("DE HAVILLAND", "De Havilland"),
                    ("MCDONNELL", "McDonnell Douglas"),
                    ("ROBIN", "Robin")
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
                    // Boeing 737 family
                    ("B737", "B737", "B737-700"),
                    ("B738", "B738", "B737-800"),
                    ("B739", "B739", "B737-900"),
                    // Boeing 747 family
                    ("B741", "B741", "B747-100"),
                    ("B742", "B742", "B747-200"),
                    ("B743", "B743", "B747-300"),
                    ("B744", "B744", "B747-400"),
                    ("B748", "B748", "B747-8"),
                    // Boeing 757 family
                    ("B752", "B752", "B757-200"),
                    ("B753", "B753", "B757-300"),
                    // Boeing 767 family
                    ("B762", "B762", "B767-200"),
                    ("B763", "B763", "B767-300"),
                    ("B764", "B764", "B767-400"),
                    // Boeing 777 family
                    ("B772", "B772", "B777-200ER"),
                    ("B77W", "B77W", "B777-300ER"),
                    ("B77F", "B77F", "B777 Freighter"),
                    ("B77L", "B77L", "B777-200LR"),
                    // Boeing 787 family
                    ("B788", "B788", "B787-8"),
                    ("B789", "B789", "B787-9"),
                    ("B78X", "B78X", "B787-10"),
                    // Boeing legacy
                    ("B712", "B712", "B717-200"),
                    ("B722", "B722", "B727-200"),
                    // Airbus A320 family
                    ("A318", "A318", "A318"),
                    ("A319", "A319", "A319"),
                    ("A320", "A320", "A320-200"),
                    ("A321", "A321", "A321"),
                    ("A20N", "A20N", "A320neo"),
                    ("A21N", "A21N", "A321neo"),
                    // Airbus A330 family
                    ("A332", "A332", "A330-200"),
                    ("A333", "A333", "A330-300"),
                    ("A338", "A338", "A330-800neo"),
                    ("A339", "A339", "A330-900neo"),
                    // Airbus A340 family
                    ("A343", "A343", "A340-300"),
                    ("A345", "A345", "A340-500"),
                    ("A346", "A346", "A340-600"),
                    // Airbus A350 family
                    ("A359", "A359", "A350-900"),
                    ("A35K", "A35K", "A350-1000"),
                    // Airbus A380
                    ("A388", "A388", "A380-800"),
                    // Cessna piston family
                    ("C150", "C150", "C150"),
                    ("C152", "C152", "C152"),
                    ("C162", "C162", "C162 Skycatcher"),
                    ("C172", "C172", "C172 Skyhawk"),
                    ("C180", "C180", "C180"),
                    ("C182", "C182", "C182 Skylane"),
                    // Cessna Caravan
                    ("C208", "C208", "C208 Caravan"),
                    // Cessna Citation jet family
                    ("C25A", "C25A", "Citation CJ2"),
                    ("C25B", "C25B", "Citation CJ3"),
                    ("C25C", "C25C", "Citation CJ4"),
                    ("C525", "C525", "Citation CJ1"),
                    ("C550", "C550", "Citation II"),
                    ("C560", "C560", "Citation V"),
                    ("C680", "C680", "Citation Sovereign"),
                    ("C700", "C700", "Citation Longitude"),
                    // Piper family
                    ("PA28", "PA28", "Piper Cherokee"),
                    ("PA34", "PA34", "Piper Seneca"),
                    ("PA44", "PA44", "Piper Seminole"),
                    ("PA46", "PA46", "Piper Malibu"),
                    // Embraer E-jets
                    ("E170", "E170", "Embraer 170"),
                    ("E175", "E175", "Embraer 175"),
                    ("E190", "E190", "Embraer 190"),
                    ("E195", "E195", "Embraer 195"),
                    // Embraer business jets
                    ("E35L", "E35L", "Legacy 600"),
                    ("E50P", "E50P", "Phenom 100"),
                    ("E55P", "E55P", "Phenom 300"),
                    // Bombardier CRJ family
                    ("CRJ2", "CRJ2", "CRJ-200"),
                    ("CRJ7", "CRJ7", "CRJ-700"),
                    ("CRJ9", "CRJ9", "CRJ-900"),
                    ("CRJX", "CRJX", "CRJ-1000"),
                    // Bombardier / Airbus A220
                    ("BCS1", "BCS1", "A220-100"),
                    ("BCS3", "BCS3", "A220-300"),
                    // Bombardier Global
                    ("GL5T", "GL5T", "Global 5000"),
                    ("GLEX", "GLEX", "Global Express"),
                    // Learjet
                    ("LJ35", "LJ35", "Learjet 35"),
                    ("LJ60", "LJ60", "Learjet 60"),
                    ("LJ75", "LJ75", "Learjet 75"),
                    // ATR family
                    ("AT42", "AT42", "ATR 42-300"),
                    ("AT43", "AT43", "ATR 42-300"),
                    ("AT45", "AT45", "ATR 42-500"),
                    ("AT72", "AT72", "ATR 72-200"),
                    ("AT75", "AT75", "ATR 72-500"),
                    ("AT76", "AT76", "ATR 72-600"),
                    // De Havilland
                    ("DH8A", "DH8A", "Dash 8-100"),
                    ("DH8C", "DH8C", "Dash 8-300"),
                    ("DH8D", "DH8D", "Dash 8-400"),
                    ("DHC6", "DHC6", "Twin Otter"),
                    // McDonnell Douglas
                    ("MD11", "MD11", "MD-11"),
                    ("MD82", "MD82", "MD-82"),
                    ("MD83", "MD83", "MD-83"),
                    // Daher TBM
                    ("TBM7", "TBM7", "TBM 700"),
                    ("TBM8", "TBM8", "TBM 850"),
                    ("TBM9", "TBM9", "TBM 930"),
                    // Cirrus
                    ("SR20", "SR20", "SR20"),
                    ("SR22", "SR22", "SR22"),
                    ("SR2T", "SR2T", "SR22T"),
                    // Beechcraft
                    ("BE36", "BE36", "Bonanza G36"),
                    ("BE58", "BE58", "Baron 58"),
                    ("BE60", "BE60", "Duke"),
                    ("B350", "B350", "King Air 350"),
                    ("B06T", "B06T", "King Air C90"),
                    // Pilatus
                    ("PC12", "PC12", "PC-12"),
                    ("PC24", "PC24", "PC-24"),
                    // Fokker
                    ("F100", "F100", "Fokker 100"),
                    ("F28", "F28", "Fokker 28"),
                    // HondaJet
                    ("HDJT", "HDJT", "HondaJet"),
                    // Light / Electric / Sport
                    ("PIVI", "PIVI", "Pipistrel Velis"),
                    ("ICON", "ICON", "ICON A5"),
                    ("DRCO", "DRCO", "Robin DR400"),
                    // Military
                    ("F18H", "F18H", "F/A-18E Super Hornet"),
                    ("F16C", "F16C", "F-16C Fighting Falcon"),
                    ("A10", "A10", "A-10 Warthog"),
                    ("B52", "B52", "B-52 Stratofortress"),
                    ("C130", "C130", "C-130 Hercules")
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
