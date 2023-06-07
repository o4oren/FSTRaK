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
        public static readonly List<string> C172 = new List<string>(new string[] { "C172", "172", "C140", "C170", "C210", "C182", "C177" });
        public static readonly List<string> B747 = new List<string>(new string[] { "742", "741", "747", "743", "744", "B748" });
        public static readonly List<string> B767 = new List<string>(new string[] { "767", "762", "763", "764" });
        public static readonly List<string> B777 = new List<string>(new string[] { "777", "772", "773", "778", "779", "77X", "77L", "77W" });
        public static readonly List<string> B787 = new List<string>(new string[] { "787", "788", "789", "78X" });
        public static readonly List<string> A340 = new List<string>(new string[] { "340", "343", "345", "346", "347" });
        public static readonly List<string> A330 = new List<string>(new string[] { "330", "332", "333", "339", "A310", "A300", "33X", "33Y", "359", "35K" });
        public static readonly List<string> A380 = new List<string>(new string[] { "380", "388" });
        public static readonly List<string> ERJ = new List<string>(new string[]
        {
            "E175", "E195", "CRJ", "CJ2", "CJ3", "Citation", "Honda", "CJ4", "Lear", "RJ", "C500", "C510", "C525", "C550", "C560",
            "CL30", "CL60", "C25C"
        });
        public static readonly List<string> DC3 = new List<string>(new string[]
        {
            "DC3", "DC-3", "C47", "DC2"
        });

        public static System.Drawing.Color GetColorFromResource(string name)
        {
            var mColor = (System.Windows.Media.Color)Application.Current.Resources[name];
            return System.Drawing.Color.FromArgb(mColor.A, mColor.R, mColor.G, mColor.B);
        }

        public static string GetAircraftIcon(Aircraft aircraft)
        {
            // Match on type first
            if (B737IconCandidates.Any(aircraft.AircraftType.Contains))
                return "B737";

            if (A320IconCandidates.Any(aircraft.AircraftType.Contains))
                return "A320";

            if (C172.Any(aircraft.AircraftType.Contains))
                return "C172";

            if (B747.Any(aircraft.AircraftType.Contains))
                return "B747";


            if (B767.Any(aircraft.AircraftType.Contains))
                return "B767";


            if (B777.Any(aircraft.AircraftType.Contains))
                return "B777";


            if (B787.Any(aircraft.AircraftType.Contains))
                return "B787";

            if (A340.Any(aircraft.AircraftType.Contains))
                return "A340";

            if (A330.Any(aircraft.AircraftType.Contains))
                return "A330";

            if (A380.Any(aircraft.AircraftType.Contains))
                return "A380";

            if (ERJ.Any(aircraft.AircraftType.Contains))
                return "ERJ";


            // If not matched on the type, try other heuristics
            if (aircraft.NumberOfEngines == 1 && aircraft.EngineType == EngineType.Piston)
            {
                return "C172";
            }

            if (aircraft.NumberOfEngines == 2 && aircraft.EngineType == EngineType.Piston)
            {
                return "DC3";
            }

            if (aircraft.NumberOfEngines == 4 && aircraft.EngineType == EngineType.Jet)
            {
                return "A340";
            }

            return "B737";
        }
    }
}
