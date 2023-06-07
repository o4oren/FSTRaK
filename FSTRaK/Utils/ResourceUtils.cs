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
        public static readonly List<string> B747 = new List<string>(new string[] { "742", "741", "747", "743", "744" });

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


            // If not matched on the type, try other heuristics
            if (aircraft.NumberOfEngines == 1 && aircraft.EngineType == EngineType.Piston)
            {
                return "C172";
            }

            if (aircraft.NumberOfEngines == 2 && aircraft.EngineType == EngineType.Piston)
            {
                return "Dc3";
            }

            if (aircraft.NumberOfEngines == 4 && aircraft.EngineType == EngineType.Jet)
            {
                return "A340";
            }

            return "B737";
        }
    }
}
