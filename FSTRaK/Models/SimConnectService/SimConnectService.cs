using FSTRaK.DataTypes;
using Microsoft.FlightSimulator.SimConnect;
using Serilog;
using System;
using System.ComponentModel;
using System.Device.Location;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Interop;

namespace FSTRaK
{
    /// <summary>
    ///    This class is a facade over simconnect and simplifies communication with the simulator for the consumer's 
    ///    interaction with the sim.
    ///    It hides the simconnect details, handles connection to the sim and exposes data.
    /// </summary>

    internal sealed class SimConnectService : INotifyPropertyChanged
    {
        private const int CONNECTION_INTERVAL = 10000;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private const string MAIN_MENU_FLT = "flights\\other\\MainMenu.FLT";
        private SimConnect _simconnect = null;

        private HwndSource gHs;
        Timer _connectionTimer;
        private IntPtr lHwnd;

        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected; 
            private set
            {
                if (value != _isConnected)
                {
                    _isConnected = value;
                    OnPropertyChanged(nameof(IsConnected));
                }
            }
        }

        private bool _isInFlight = false;
        public bool IsInFlight
        {
            get => _isInFlight; 
            private set
            {
                if(value != _isInFlight)
                {
                    _isInFlight = value;
                    OnPropertyChanged(nameof(IsInFlight));
                }
            }
        }
        
        // PAUSE_STATE_FLAG_OFF 0 
        // PAUSE_STATE_FLAG_PAUSE 1 // "full" Pause (sim + traffic + etc...) 
        // PAUSE_STATE_FLAG_PAUSE_WITH_SOUND 2 // FSX Legacy Pause (not used anymore) 
        // PAUSE_STATE_FLAG_ACTIVE_PAUSE 4 // Pause was activated using the "Active Pause" Button 
        // PAUSE_STATE_FLAG_SIM_PAUSE 8 // Pause the player sim but traffic, multi, etc... will still run
        private uint _pauseState = 1;
        public uint PauseState
        {
            get => _pauseState;
            private set
            {
                if (value != _pauseState)
                {
                    _pauseState = value;
                    OnPropertyChanged(nameof(PauseState));
                }
            }
        }

        private string _loadedFlight = string.Empty;
        public string LoadedFlight
        {
            get => _loadedFlight;
            private set
            {
                if (value != _loadedFlight)
                {
                    _loadedFlight = value;
                    OnPropertyChanged(nameof(LoadedFlight));
                }
            }
        }



        private AircraftFlightData _flightData;
        public AircraftFlightData FlightData
        {
            get
            {
                return _flightData;
            }
            private set
            {
                _flightData = value;
                OnPropertyChanged();
            }
        }

        private double _nearestAirportDistance = Double.MaxValue;
        private string _nearestAirport = string.Empty;
        public string NearestAirport
        {
            get
            {
                return _nearestAirport;
            }
            private set
            {
                _nearestAirport = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private SimConnectService(){ }
        private static readonly object _lock = new object();
        private static SimConnectService instance = null;
        public static SimConnectService Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new SimConnectService();
                    }
                    return instance;
                }
            }
        }

        /// <summary>
        /// Initialize should only be called after a main window is loaded, as it relies on it's existance for recieving system events in a wpf application.
        /// </summary>
        internal void Initialize()
        {
            //  Create a handle and hook to recieve windows messages
            WindowInteropHelper lWih = new WindowInteropHelper(System.Windows.Application.Current.MainWindow);
            lHwnd = lWih.Handle;
            gHs = HwndSource.FromHwnd(lHwnd);
            gHs.AddHook(new HwndSourceHook(WndProc));

            setConnectionTimer();
            waitForSimConnection();
        }

        private void waitForSimConnection()
        {
            ConnectToSimulator();
            if (_simconnect == null)
            {
                _connectionTimer.Start();
            }
        }

        private void setConnectionTimer()
        {
            _connectionTimer = new Timer(CONNECTION_INTERVAL);
            _connectionTimer.Elapsed += (sender, e) => ConnectToSimulator();
            _connectionTimer.AutoReset = true;
        }

        private void ConnectToSimulator()
        {
            try
            {
                Log.Debug("Trying to connect");
                _simconnect = new SimConnect("FSTrAk", lHwnd, WM_USER_SIMCONNECT, null, 0);
                if (_simconnect != null)
                {
                    ConfigureSimconnect();
                }
            }
            catch (COMException ex)
            {
                // Do nothing
            }
        }

        private void ConfigureSimconnect()
        {
            // Management events
            _simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
            _simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
            _simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

            // Configure and register data DataDefinitions for requests
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Year", "number", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Month of Year", "number", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Day of Month", "number", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Zulu Time", "seconds", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Airline", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Model", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Type", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Sim On Ground", "Bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Heading Degrees True", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Airspeed True", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Airspeed Indicated", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Ground Altitude", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Alt Above Ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Alt Above Ground Minus CG", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Vertical Speed", "ft/min", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Camera State", null, SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Flap Speed Exceeded", null, SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Gear Speed Exceeded", null, SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Overspeed Warning", null, SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "TRAILING EDGE FLAPS LEFT ANGLE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "FUEL TOTAL QUANTITY WEIGHT", "pounds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            _simconnect.RegisterDataDefineStruct<AircraftFlightData>(DataDefinitions.FlightData);

            // Subscribe to System events
            _simconnect.SubscribeToSystemEvent(EVENTS.FLIGHT_LOADED, "FlightLoaded");
            _simconnect.SubscribeToSystemEvent(EVENTS.PAUSE, "Pause_EX1");

            // Register listeners on simconnect events
            _simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
            _simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(simconnect_OnRecvAirportList);
            _simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simconnect_OnRecvEvent);
            _simconnect.OnRecvEventFilename += new SimConnect.RecvEventFilenameEventHandler(simconnect_OnRecvFilename);
            _simconnect.OnRecvSystemState  += new SimConnect.RecvSystemStateEventHandler(simconnect_OnRecvSystemState);

            // Start getting data
            _simconnect.RequestSystemState(Requests.FlightLoaded, "FlightLoaded");
            _simconnect.RequestDataOnSimObject(Requests.FlightDataRequest, DataDefinitions.FlightData, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);

        }

        private void simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
        {
            if (data.dwRequestID == (uint)Requests.FlightLoaded)
            {
                LoadedFlight = data.szString;
                Log.Debug(LoadedFlight);
            }
        }

        private void simconnect_OnRecvFilename(SimConnect sender, SIMCONNECT_RECV_EVENT_FILENAME data)
        {
            LoadedFlight = data.szFileName;
            Log.Debug(LoadedFlight);
        }

        private void simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch (data.uEventID)
            {
                case (int)EVENTS.FLIGHT_LOADED:
                    Log.Debug($"=== Loaded {data.dwData.ToString()}");
                    // Do nothing, this is handled in OnRecvFileName
                    break;
                case (int)EVENTS.PAUSE:
                    PauseState = data.dwData;
                    break;
            }
        }

        private void UpdateInFlightState()
        {
            if (!LoadedFlight.Equals(MAIN_MENU_FLT) && PauseState != 1)
            {
                IsInFlight = true;
            }
            else
            {
                IsInFlight = false;
            }
        }

        private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Log.Information("Sim connection closed!");
            Close();
            IsConnected = false;
            _connectionTimer.Start();
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Log.Information("Sim connection Openned!");
            _connectionTimer.Stop();
            IsConnected = true;
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Log.Error(data.dwException.ToString());
        }

        private void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            FlightData = (AircraftFlightData)data.dwData[0];
            OnPropertyChanged(nameof(FlightData));
        }

        public void RequestNearestAirport()
        {
            _nearestAirportDistance = Double.MaxValue;
            _simconnect.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, Requests.NearbyAirportsRequest);
        }

        private void simconnect_OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
        {
            try
            {
                var myCoordinates = new GeoCoordinate(FlightData.latitude, FlightData.longitude);

                if (myCoordinates != null)
                {
                    foreach (SIMCONNECT_DATA_FACILITY_AIRPORT a in data.rgData.Cast<SIMCONNECT_DATA_FACILITY_AIRPORT>())
                    {
                        var airportCoord = new GeoCoordinate(a.Latitude, a.Longitude);
                        var distance = airportCoord.GetDistanceTo(myCoordinates);
                        if (distance < _nearestAirportDistance)
                        {
                            if(a.Icao != NearestAirport)
                            {
                                NearestAirport = a.Icao;
                                _nearestAirportDistance = distance;
                                Log.Information($"Closest found airport is {NearestAirport}");
                            }
                        }
                    }
                }
            } catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if(name.Equals(nameof(LoadedFlight)) || name.Equals(nameof(PauseState)))
            {
                UpdateInFlightState();
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            // If message is coming from simconnect and the connection is not null;
            // Continue and receive message.
            if (msg == WM_USER_SIMCONNECT && _simconnect != null)
            {
                _simconnect.ReceiveMessage();
                handled = true;
            }
            return (IntPtr)0;
        }

        public void Close()
        {
            if (_simconnect != null)
            {
                _simconnect.Dispose();
                _simconnect = null;
            }
            Log.Debug("SimConnect Disposed!");
        }
    }
}
