using System;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.FlightSimulator.SimConnect;
using Serilog;

namespace FSTRaK
{
    /**
     * This class represents the connection of FSTRaK with the simulator.
     * 
     */
    internal class SimConnectManager : INotifyPropertyChanged
    {
        const int WM_USER_SIMCONNECT = 0x0402;
        private SimConnect _simconnect = null;
        private GeoCoordinate myCoordinates = null;
        private HwndSource gHs;

        private AircraftFlightData flightData;
        public AircraftFlightData FlightData
        {
            get {
                return flightData;
            }
            set {
                flightData = value;
                OnPropertyChanged();
} 
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private SimConnectManager() { }
        private static readonly object _lock = new object ();  
        private static SimConnectManager instance = null;
        public static SimConnectManager Instance
        {
            get
            {
                lock (_lock)
                    {
                        if (instance == null)
                        {
                            instance = new SimConnectManager();
                        }
                        return instance;
                    }
            }
        }


        private enum DATA_REQUESTS
        {
            FLIGHT_METADATA_REQUEST,
            AIRCRAFT_FLIGHT_DATA_REQUEST,
            NEARBY_AIRPORTS_REQUEST
        }

        private enum DEFINITIONS
        {
            FLIGHT_METADATA,
            AIRCRAFT_FLIGHT_DATA,
            NEARBY_AIRPORTS
        }

        private enum EVENTS
        {
            CRASHED,
            SIM_START,
            SIM_STOP,
            SIM
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftFlightData
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x100)]
            public string title;
            public double latitude;
            public double longitude;
            public double trueHeading;
            public double altitude;
            public double airspeed;

        }


        internal void Initialize()
        {
            try
            {
                //  Create a handle and hook to recieve windows messages
                WindowInteropHelper lWih = new WindowInteropHelper(System.Windows.Application.Current.MainWindow);
                IntPtr lHwnd = lWih.Handle;
                gHs = HwndSource.FromHwnd(lHwnd);
                gHs.AddHook(new HwndSourceHook(WndProc));
                _simconnect = new SimConnect("Managed Data Request", lHwnd, WM_USER_SIMCONNECT, null, 0);
                if (_simconnect != null)
                {
                    // Management events
                    _simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                    _simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
                    _simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                    // Register listeners and configure data definitions
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Plane Heading Degrees True", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simconnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_FLIGHT_DATA, "Airspeed True", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                    _simconnect.RegisterDataDefineStruct<AircraftFlightData>(DEFINITIONS.AIRCRAFT_FLIGHT_DATA);
                    _simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
                    _simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(simconnect_OnRecvAirportList);
                    _simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simconnect_OnRecvEvent);
                    
                    // Start getting data
                    _simconnect.RequestDataOnSimObject(DATA_REQUESTS.AIRCRAFT_FLIGHT_DATA_REQUEST, DEFINITIONS.AIRCRAFT_FLIGHT_DATA, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);
                    _simconnect.SubscribeToSystemEvent(EVENTS.SIM_START, "SimStart");
                    _simconnect.SubscribeToSystemEvent(EVENTS.SIM_STOP, "SimStop");
                    _simconnect.SubscribeToSystemEvent(EVENTS.CRASHED, "Crashed");
                    _simconnect.SubscribeToSystemEvent(EVENTS.SIM, "Sim");

                }
            }
            catch (COMException ex)
            {
                Log.Error("Failed to connect to MS Flight Simulator!");
            }

            void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
            {
                Log.Debug("Openned!");
            }

            void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
            {
                Log.Error(data.dwException.ToString());
            }
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
            }
        }

        private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Log.Debug("Quit!");

            //if (simconnect != null)
            //{
            //    simconnect.Dispose();
            //    simconnect = null;
            //}
        }

        private void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            FlightData = (AircraftFlightData)data.dwData[0];

            myCoordinates = new GeoCoordinate(FlightData.latitude, FlightData.longitude);
            // Log.Information($"{a.title} is at {myCoordinates} heading: {a.trueHeading} at alt: {a.altitude}");

            if (myCoordinates != null)
            {
                _simconnect.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, DATA_REQUESTS.NEARBY_AIRPORTS_REQUEST);
            }
            Log.Information($"Closest Airport: {closest} Distance: {dist} meters, there are {c} airports");
        }

        private int c = 0;
        private String closest = "";
        private double dist = double.MaxValue;

        

        private void simconnect_OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
        {
            c += data.rgData.Length;
            if (myCoordinates != null)
            {
                foreach (SIMCONNECT_DATA_FACILITY_AIRPORT a in data.rgData.Cast<SIMCONNECT_DATA_FACILITY_AIRPORT>())
                {
                    var airportCoord = new GeoCoordinate(a.Latitude, a.Longitude);
                    var distance = airportCoord.GetDistanceTo(myCoordinates);
                    if (distance < dist)
                    {
                        closest = a.Icao;
                        dist = distance;
                    }
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
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
    }
}
