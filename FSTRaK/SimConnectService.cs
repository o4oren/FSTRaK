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
        private SimConnect _simconnect = null;
        private GeoCoordinate myCoordinates = null;

        private HwndSource gHs;
        private DateTime _lastUpdated = DateTime.Now;
        private string _loadedFlight = string.Empty;
        Timer _connectionTimer;
        private IntPtr lHwnd;


        private AircraftFlightData _flightData;
        public AircraftFlightData FlightData
        {
            get
            {
                return _flightData;
            }
            set
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
            set
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


        private enum DataRequests
        {
            FlightDataRequest,
            NearbyAirportsRequest
        }

        private enum DataDefinitions
        {
            FlightMetaData,
            FlightData,
            NearbyAirports
        }

        private enum EVENTS
        {
            CRASHED,
            SIM_START,
            SIM_STOP,
            SIM,
            FLIGHT_LOADED,
            PAUSE,
            AIRCRAFT_LOADED,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftFlightData
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string title;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string airline;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string model;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string atcType;

            public double latitude;
            public double longitude;
            public double trueHeading;
            public double altitude;
            public double airspeed;
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
                Log.Error(ex.InnerException.ToString());
            }
        }

        private void ConfigureSimconnect()
        {
            // Management events
            _simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
            _simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
            _simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

            // Register listeners and configure data DataDefinitions
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Airline", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Model", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "ATC Type", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Heading Degrees True", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Airspeed True", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            // register methods and subscribe to events
            _simconnect.RegisterDataDefineStruct<AircraftFlightData>(DataDefinitions.FlightData);
            _simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
            _simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(simconnect_OnRecvAirportList);
            _simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simconnect_OnRecvEvent);
            _simconnect.OnRecvEventFilename += new SimConnect.RecvEventFilenameEventHandler(simconnect_OnRecvFilename);
            _simconnect.SubscribeToSystemEvent(EVENTS.SIM_START, "SimStart");
            _simconnect.SubscribeToSystemEvent(EVENTS.SIM_STOP, "SimStop");
            _simconnect.SubscribeToSystemEvent(EVENTS.CRASHED, "Crashed");
            _simconnect.SubscribeToSystemEvent(EVENTS.SIM, "Sim");
            _simconnect.SubscribeToSystemEvent(EVENTS.FLIGHT_LOADED, "FlightLoaded");
            _simconnect.SubscribeToSystemEvent(EVENTS.PAUSE, "Pause_EX1");

            // Start getting data
            _simconnect.RequestDataOnSimObject(DataRequests.FlightDataRequest, DataDefinitions.FlightData, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);

        }

        private void simconnect_OnRecvFilename(SimConnect sender, SIMCONNECT_RECV_EVENT_FILENAME data)
        {
            _loadedFlight = data.szFileName;
            Log.Debug(_loadedFlight);
        }

        private void simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch (data.uEventID)
            {
                case (int)EVENTS.CRASHED:
                    Log.Debug("=== crashed");
                    break;
                case (int)EVENTS.SIM_START:
                    Log.Debug("=== Sim Start");
                    break;
                case (int)EVENTS.SIM_STOP:
                    Log.Debug("=== Sim Stop");
                    break;
                case (int)EVENTS.SIM:
                    Log.Debug($"=== Sim {data.dwData.ToString()}");
                    break;
                case (int)EVENTS.FLIGHT_LOADED:
                    Log.Debug($"=== Loaded {data.dwData.ToString()}");
                    break;
                case (int)EVENTS.PAUSE:
                    Log.Debug($"=== Pause type {data.dwData.ToString()}");
                    break;
            }
        }

        private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Log.Information("Sim connection closed!");
            Close();
            _connectionTimer.Start();
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Log.Information("Sim connection Openned!");
            _connectionTimer.Stop();
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Log.Error(data.dwException.ToString());
        }

        private void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (_lastUpdated.AddSeconds(0.1) < DateTime.Now)
            {
                _lastUpdated = DateTime.Now;
                FlightData = (AircraftFlightData)data.dwData[0];
                OnPropertyChanged(nameof(FlightData));

                // Change to request this on flight start and end
                if(NearestAirport == string.Empty) 
                    RequestNearestAirport();
            }
        }

        public void RequestNearestAirport()
        {
            _nearestAirportDistance = Double.MaxValue;
            _simconnect.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, DataRequests.NearbyAirportsRequest);
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            // if message is coming from simconnect and the connection is not null;
            // continue and receive message
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
