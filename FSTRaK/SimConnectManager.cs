using System;
using System.Device.Location;
using System.Diagnostics;
using System.Linq;
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
    internal class SimConnectManager
    {
        const int WM_USER_SIMCONNECT = 0x0402;
        private SimConnect simconnect = null;
        private GeoCoordinate myCoordinates = null;
        private HwndSource gHs;

        private enum DATA_REQUESTS
        {
            AIRCRAFT_INFO_REQUEST,
            AIRPORTS_REQUEST
        }

        private enum DEFINITIONS
        {
            AircraftInfo,
            AIRPORTS_REQUEST
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AircraftInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x100)]
            public string title;
            public double latitude;
            public double longitude;
            public double trueheading;
            public double groundaltitude;
            public double altitude;

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
                simconnect = new SimConnect("Managed Data Request", lHwnd, WM_USER_SIMCONNECT, null, 0);
                if (simconnect != null)
                {
                    // Management events
                    simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                    simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);
                    simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                    // Register listeners and configure data definitions
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Plane Heading Degrees True", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Above Ground Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                    simconnect.RegisterDataDefineStruct<AircraftInfo>(DEFINITIONS.AircraftInfo);
                    simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
                    simconnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(simconnect_OnRecvAirportList);

                    // Start getting data
                    simconnect.RequestDataOnSimObject(DATA_REQUESTS.AIRCRAFT_INFO_REQUEST, DEFINITIONS.AircraftInfo, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);
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
            Log.Debug("data recieved!");
            Log.Debug(((AircraftInfo)data.dwData[0]).title);
            myCoordinates = new GeoCoordinate(((AircraftInfo)data.dwData[0]).latitude, ((AircraftInfo)data.dwData[0]).longitude);
            Log.Debug($"My coords: {myCoordinates.Latitude} : {myCoordinates.Longitude}");
            if (myCoordinates != null)
            {
                simconnect.RequestFacilitiesList_EX1(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, DATA_REQUESTS.AIRPORTS_REQUEST);
            }
            Console.WriteLine($"Closest Airport: {closest} Distance: {dist} meters, there are {c} airports");
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

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            // if message is coming from simconnect and the connection is not null;
            // continue and receive message
            if (msg == WM_USER_SIMCONNECT && simconnect != null)
            {
                simconnect.ReceiveMessage();
                handled = true;
            }
            return (IntPtr)0;
        }
    }
}
