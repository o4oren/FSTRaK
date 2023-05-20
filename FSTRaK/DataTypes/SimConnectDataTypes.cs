using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FSTRaK.DataTypes
{


    // This enum represents camera states as defined on https://docs.flightsimulator.com/html/Programming_Tools/SimVars/Camera_Variables.htm#CAMERA_STATE 
    public enum CameraState
    {
        Cockpit = 2,
        External = 3,
        Drone = 4,
        Fixed = 5,
        Environment = 6,
        SixDof = 7,
        GamePlay = 8,
        Showcase = 9,
        DroneAircraft = 10,
        Waiting = 11,
        WorldMap = 12,
        HangarRtc = 13, 
        HangarCustom = 14,
        MenuRtc = 15,
        InGameRtc = 16,
        Replay = 17,
        DroneTopDown = 19,
        Hangar = 21,
        Ground = 24,
        FollowTrafficAircraft = 25
    }

    public enum Requests
    {
        FlightDataRequest,
        NearbyAirportsRequest,
        FlightLoaded
    }

    public enum DataDefinitions
    {
        FlightMetaData,
        FlightData,
        NearbyAirports
    }

    public enum EVENTS
    {
        FLIGHT_LOADED,
        PAUSE,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftFlightData
    {
        public int zuluYear;
        public int zuluMonth;
        public int zuluDay;
        public int zuluTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string title;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string airline;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string atcType;

        public bool simOnGround;
        public double latitude;
        public double longitude;
        public double trueHeading;
        public double altitude;
        public double trueAirspeed;
        public double indicatedAirpeed;
        public double groundVelocity;
        public double groundAltitude;
        public double planeAltAboveGround;
        public double planeAltAboveGroundMinusCg;
        public double verticalSpeed;
        public int CameraState;
        public bool FlapSpeedExceeded;
        public bool GearSpeedExceeded;
        public bool Overspeed;
    }
    internal class SimConnectDataTypes
    {
         
    }
}
