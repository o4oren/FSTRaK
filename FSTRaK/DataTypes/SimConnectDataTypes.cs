
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

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
        Waiting = 11,       // Loading flight
        WorldMap = 12,
        HangarRtc = 13, 
        HangarCustom = 14,
        MenuRtc = 15,       // Main menu
        InGameRtc = 16,     // "Ready to fly"
        Replay = 17,
        DroneTopDown = 19,
        Hangar = 21,
        Ground = 24,
        FollowTrafficAircraft = 25,
        LoadingFlight3D2024 = 30,
        MainMenu2024 = 32,
        InFlightMenu2024 = 29,
        InFlightMenu2024_3 = 34,
        InFlightMenu2024_2 = 35,
        SomethingInLoadingProcess2024 = 36

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
        FlightLoaded,
        AircraftLoaded,
        AircraftDataRequest,
        SimVersionRequest
    }

    public enum DataDefinitions
    {
        AircraftData,
        FlightData
    }

    public enum Events
    {
        FlightLoaded,
        AircraftLoaded,
        Pause,
        Crashed,
        Sim,
        View
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftData
    {
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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Category;

        public EngineType EngineType;
        public int NumberOfEngines;
        public double EmptyWeightLbs;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string liveryName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct FlightData
    {
        public int zuluYear;
        public int zuluMonth;
        public int zuluDay;
        public int zuluTime;

        public int SimOnGround;
        public double Latitude;
        public double Longitude;
        public double TrueHeading;
        public double Altitude;
        public double TrueAirspeed;
        public double IndicatedAirspeed;
        public double GroundVelocity;
        public double GroundAltitude;
        public double PlaneAltAboveGround;
        public double PlaneAltAboveGroundMinusCg;
        public double VerticalSpeed;
        public CameraState CameraState;
        public int FlapSpeedExceeded;
        public int GearSpeedExceeded;
        public int OverSpeed;
        public int StallWarning;
        public double FlapPosition;
        public double FuelWeightLbs;
        public double TotalWeightLbs;
        public int ParkingBrakesSet;

        public double Engine1MaxRpmPct;
        public double Engine2MaxRpmPct;
        public double Engine3MaxRpmPct;
        public double Engine4MaxRpmPct;

        public double Throttle1Position;
        public double Throttle2Position;
        public double Throttle3Position;
        public double Throttle4Position;

        /// <summary>
        /// Used to determine negine start up
        /// </summary>
        /// <returns>double - max number of started engines</returns>
        public double MaxEngineRpmPct() 
        {
            return new double[] { Engine1MaxRpmPct, Engine2MaxRpmPct, Engine3MaxRpmPct, Engine4MaxRpmPct}.Max();
        }

        public double MaxThrottlePosition()
        {
            return new double[] { Throttle1Position, Throttle2Position, Throttle3Position, Throttle3Position }.Max();
        }

        public double MinThrottlePosition(int numberOfEngines)
        {
            var throttlePositionArray = new List<double>( new double[] { Throttle1Position, Throttle1Position, Throttle2Position, Throttle3Position });
            if (numberOfEngines == 0)
                return 0;
            return throttlePositionArray.GetRange(0, numberOfEngines).Min();
        }

    }

    public interface IAirportData
    {
        string Ident { get; }
        double Latitude { get; }
        double Longitude { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class AirportData2024 : IAirportData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
        private string _ident;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 3)]
        private string _region;

        private double _latitude;
        private double _longitude;

        private double _altitude;

        public string Ident => _ident;
        public string Region => _region;

        public double Latitude => _latitude;
        public double Longitude => _longitude;
        public double Altitude => _altitude;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class AirportData2020 : IAirportData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
        private string _ident;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 3)]
        private string _region;

        private double _latitude;
        private double _longitude;

        private double _altitude;

        public string Ident => _ident;
        public string Region => _region;

        public double Latitude => _latitude;
        public double Longitude => _longitude;
        public double Altitude => _altitude;

    }

    internal class SimConnectDataTypes
    {
         
    }
}
