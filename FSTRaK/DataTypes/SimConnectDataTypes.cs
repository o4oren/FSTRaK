
using System.Linq;
using System.Runtime.InteropServices;

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

    public enum CrashFlag
    {
        None = 0,
        Mountain = 2,
        General = 4,
        Building = 6,
        Splash = 8, 
        GearUp = 10,
        Overstress = 12, 
        Building2 = 14,
        Aircraft = 16,
        FuelTruck = 18
    }

    public enum EngineType
    {
        Piston, 
        Jet, 
        None, 
        HeloTurbine,
        Unsupported,
        Turboprop
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
        CRASHED
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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string AtcId;

        public EngineType EngineType;
        public int NumberOfEngines;


        public int SimOnGround;
        public double Latitude;
        public double Longitude;
        public double TrueHeading;
        public double Altitude;
        public double TrueAirspeed;
        public double IndicatedAirpeed;
        public double GroundVelocity;
        public double GroundAltitude;
        public double PlaneAltAboveGround;
        public double PlaneAltAboveGroundMinusCg;
        public double VerticalSpeed;
        public int CameraState;
        public int FlapSpeedExceeded;
        public int GearSpeedExceeded;
        public int Overspeed;
        public double FlapPosition;
        public double FuelWeightLbs;
        public int ParkingBrakesSet;

        public double Engine1MaxRpmPct;
        public double Engine2MaxRpmPct;
        public double Engine3MaxRpmPct;
        public double Engine4MaxRpmPct;


        /// <summary>
        /// Used to determine negine start up
        /// </summary>
        /// <returns>double - max number of started engines</returns>
        public double MaxEngineRpmPct() 
        {
            return new double[] { Engine1MaxRpmPct, Engine2MaxRpmPct, Engine3MaxRpmPct, Engine4MaxRpmPct}.Max();
        }

    }
    internal class SimConnectDataTypes
    {
         
    }
}
