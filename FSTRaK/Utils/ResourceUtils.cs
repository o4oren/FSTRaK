using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.Utils
{
    internal static class ResourceUtils
    {
        public static readonly List<string> B737IconCandidates = new List<string>(new string[] { "737", "738", "739", "733", "734", "735", "736" });
        public static readonly List<string> A320IconCandidates = new List<string>(new string[] { "A318", "A319", "A320", "A-320", "A321", "A20N", "A21N" });
        public static readonly List<string> C172 = new List<string>(new string[] { "C172", "172", "C140", "C170", "C210", "C182", "C177" , "PA28", "P28A", "P28R", "P28B", "P28T", "DA20", "DA40", "SR22"});
        public static readonly List<string> B747 = new List<string>(new string[] { "742", "741", "747", "743", "744", "B748", "B74R", "B74S"});
        public static readonly List<string> B767 = new List<string>(new string[] { "767", "762", "763", "764" });
        public static readonly List<string> B777 = new List<string>(new string[] { "777", "772", "773", "778", "779", "77X", "77L", "77W" });
        public static readonly List<string> B787 = new List<string>(new string[] { "787", "788", "789", "78X" });
        public static readonly List<string> A340 = new List<string>(new string[] { "340", "343", "345", "346", "347" });
        public static readonly List<string> A330 = new List<string>(new string[] { "330", "332", "333", "339", "A310", "A306","A300", "33X", "33Y", "359", "35K" });
        public static readonly List<string> A380 = new List<string>(new string[] { "380", "388" });
        public static readonly List<string> ERJ = new List<string>(new string[]
        {
            "E175", "E190", "E170", "E195", "CRJ", "CJ2", "CJ3", "Citation", "Honda", "CJ4", "Lear", "RJ", "C500", "C510", "C525", "C550", "C560",
            "CL30", "CL60", "C25C", "GLF5", "LJ35"
        });
        public static readonly List<string> DC3 = new List<string>(new string[]
        {
            "DC3", "DC-3", "C47", "DC2"
        });

        public static readonly List<string> Helicopter = new List<string>(new string[] { "B06", "H500", "H135", "H145", "H155", "BK-117C-2", "H125", "H275", "R44", "B47G", "R66", "B06", "B212", "UH1" });

        public static System.Drawing.Color GetColorFromResource(string name)
        {
            var mColor = (System.Windows.Media.Color)Application.Current.Resources[name];
            return System.Drawing.Color.FromArgb(mColor.A, mColor.R, mColor.G, mColor.B);
        }

        public static (string, double) GetAircraftIcon(Aircraft aircraft)
        {
            if (aircraft.Category.Equals("Helicopter"))
                return ("Helicopter", 0.6);
            (var aicraftIcon, var scaleFactor) = GetAircraftIcon(aircraft.AircraftType);
            if (aicraftIcon == null)
            {
                // If not matched on the type, try other heuristics
                if (aircraft.NumberOfEngines == 1 && aircraft.EngineType == EngineType.Piston)
                {
                    return ("C172", 0.6);
                }

                if (aircraft.NumberOfEngines == 2 && aircraft.EngineType == EngineType.Piston)
                {
                    return ("DC3", 0.7);
                }

                if (aircraft.NumberOfEngines == 4 && aircraft.EngineType == EngineType.Jet)
                {
                    return ("A340", 0.9);
                }
                return ("B737", 0.75);
            }
            return (aicraftIcon, scaleFactor);
        }

        public static (string, double) GetAircraftIcon(string aircraftType)
        {
            return GetAircraftIcon(aircraftType, false);
        }
        public static (string, double) GetAircraftIcon(string aircraftType, bool isNullIfNotFound)
        {
            // Match on type first
            if (B737IconCandidates.Any(aircraftType.Contains))
                return ("B737", 0.75);

            if (A320IconCandidates.Any(aircraftType.Contains))
                return ("A320", 0.75);

            if (C172.Any(aircraftType.Contains))
                return ("C172", 0.6);

            if (B747.Any(aircraftType.Contains))
                return ("B747", 1.1);


            if (B767.Any(aircraftType.Contains))
                return ("B767", 0.8);


            if (B777.Any(aircraftType.Contains))
                return ("B777", 1);


            if (B787.Any(aircraftType.Contains))
                return ("B787", 0.9);

            if (A340.Any(aircraftType.Contains))
                return ("A340", 1);

            if (A330.Any(aircraftType.Contains))
                return ("A330", 1);

            if (A380.Any(aircraftType.Contains))
                return ("A380", 1.2);

            if (ERJ.Any(aircraftType.Contains))
                return ("ERJ", 0.6);
            
            if (DC3.Any(aircraftType.Contains))
                return ("DC3", 0.6);
            if (Helicopter.Any(aircraftType.Contains))
                return ("Helicopter", 0.6);

            if (isNullIfNotFound)
                return (null, 1);
            
            return ("B737", 0.75);
        }
    }
}
