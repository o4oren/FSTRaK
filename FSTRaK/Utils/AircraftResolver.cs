using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.Utils
{
    internal static class AircraftResolver
    {
        public static readonly List<string> B737IconCandidates = new List<string>(new string[] { "B737", "B738", "B739", "B733", "B734", "B735", "B736", "B38M", "B39M", "B3XM" });
        public static readonly List<string> A320IconCandidates = new List<string>(new string[] { "A318", "A319", "A320", "A-320", "A321", "A20N", "A21N", "A32F", "A32L", "A32N", "A32S", "T204", "B752", "B753", "B75F" });
        public static readonly List<string> C172 = new List<string>(new string[] { "C172", "C182", "C152", "C206", "P206", "SR20", "SR22", "PA22", "PA28", "PA31", "PA44", "C210", "DA40", "DA42", "DR40", "BE36" });
        public static readonly List<string> B747 = new List<string>(new string[] { "B741", "B742", "B744", "B748", "B74R", "B74S", "B74L" });
        public static readonly List<string> B767 = new List<string>(new string[] { "B762", "B763", "B764" });
        public static readonly List<string> B777 = new List<string>(new string[] { "B772", "B773", "B778", "B779", "B77X", "B77L", "B77W" });
        public static readonly List<string> B787 = new List<string>(new string[] { "B788", "B789", "B78X", "B78J", "MD11", "MD10", "DC10", "DC1F", "MD1F", "L101" });
        public static readonly List<string> A340 = new List<string>(new string[] { "A342", "A343", "A345", "A346", "IL76", "IL96" });
        public static readonly List<string> A330 = new List<string>(new string[] { "A332", "A333", "A338", "A339", "A310", "A306", "A300", "A33X", "A33Y", "A359", "A35K", "A350", "A351" });
        public static readonly List<string> A380 = new List<string>(new string[] { "A388", "A389" });
        public static readonly List<string> ERJ = new List<string>(new string[]
        {
            "CRJ1", "CRJ2", "CRJ7", "CRJX", "CRJ9", "CJ", "GLF5", "LJ35", "C25C", "C510", "C550", "C560",
            "C25B", "C56X", "C500", "C700", "C750", "C650", "F2TH", "FA50", "F27", "F28", "CL60", "HDJT"
        });
        public static readonly List<string> B200 = new List<string>(new string[]
        {
            "B200", "B300", "BE58", "BE20", "BE9L", "BE99", "BE10", "PA34", "PA42",
            "DH8D", "DH8A", "DH8B", "DH8C", "AT43", "AT45", "AT72", "AT73", "AT75", "AT76",
            "JS31", "JS32", "JS41", "SF34", "SW4", "E110", "L410", "DHC6", "BN2P"
        });
        public static readonly List<string> DC3 = new List<string>(new string[] { "DC3", "C47", "BE18" });
        public static readonly List<string> Helicopter = new List<string>(new string[] { "R22", "R44", "R66", "AS50", "AS60", "H125", "EC45", "B06", "H500", "H135" });
        public static readonly List<string> A400 = new List<string>(new string[] { "A400", "C130", "C30J", "C17" });
        public static readonly List<string> Conc = new List<string>(new string[] { "CONC" });
        public static readonly List<string> F16 = new List<string>(new string[] { "F16", "F15", "F18", "F18S" });
        public static readonly List<string> F35 = new List<string>(new string[] { "F35", "F22" });
        public static readonly List<string> C208 = new List<string>(new string[] { "C208", "K100", "K900", "PC6T", "PC6P" });

        private static readonly (string Key, string Canonical)[] ManufacturerMappings =
        {
            ("BOEING", "Boeing"),
            ("AIRBUS", "Airbus"),
            ("CESSNA", "Cessna"),
            ("PIPER", "Piper"),
            ("EMBRAER", "Embraer"),
            ("BOMBARDIER", "Bombardier"),
            ("ATR", "ATR"),
            ("DAHER", "Daher"),
            ("CIRRUS", "Cirrus"),
            ("BEECHCRAFT", "Beechcraft"),
            ("PILATUS", "Pilatus"),
            ("FOKKER", "Fokker"),
            ("LEARJET", "Learjet"),
            ("HONDA", "Honda"),
            ("DE HAVILLAND", "De Havilland"),
            ("MCDONNELL", "McDonnell Douglas"),
            ("ROBIN", "Robin")
        };

        private static readonly (string Key, string Type, string Model)[] TypeMappings =
        {
            // Boeing 737 family
            ("B737", "B737", "B737-700"),
            ("B738", "B738", "B737-800"),
            ("B739", "B739", "B737-900"),
            // Boeing 747 family
            ("B741", "B741", "B747-100"),
            ("B742", "B742", "B747-200"),
            ("B743", "B743", "B747-300"),
            ("B744", "B744", "B747-400"),
            ("B748", "B748", "B747-8"),
            // Boeing 757 family
            ("B752", "B752", "B757-200"),
            ("B753", "B753", "B757-300"),
            // Boeing 767 family
            ("B762", "B762", "B767-200"),
            ("B763", "B763", "B767-300"),
            ("B764", "B764", "B767-400"),
            // Boeing 777 family
            ("B772", "B772", "B777-200ER"),
            ("B77W", "B77W", "B777-300ER"),
            ("B77F", "B77F", "B777 Freighter"),
            ("B77L", "B77L", "B777-200LR"),
            // Boeing 787 family
            ("B788", "B788", "B787-8"),
            ("B789", "B789", "B787-9"),
            ("B78X", "B78X", "B787-10"),
            // Boeing legacy
            ("B712", "B712", "B717-200"),
            ("B722", "B722", "B727-200"),
            // Airbus A320 family
            ("A318", "A318", "A318"),
            ("A319", "A319", "A319"),
            ("A320", "A320", "A320-200"),
            ("A321", "A321", "A321"),
            ("A20N", "A20N", "A320neo"),
            ("A21N", "A21N", "A321neo"),
            // Airbus A330 family
            ("A332", "A332", "A330-200"),
            ("A333", "A333", "A330-300"),
            ("A338", "A338", "A330-800neo"),
            ("A339", "A339", "A330-900neo"),
            // Airbus A340 family
            ("A343", "A343", "A340-300"),
            ("A345", "A345", "A340-500"),
            ("A346", "A346", "A340-600"),
            // Airbus A350 family
            ("A359", "A359", "A350-900"),
            ("A35K", "A35K", "A350-1000"),
            // Airbus A380
            ("A388", "A388", "A380-800"),
            // Cessna piston family
            ("C150", "C150", "C150"),
            ("C152", "C152", "C152"),
            ("C162", "C162", "C162 Skycatcher"),
            ("C172", "C172", "C172 Skyhawk"),
            ("C180", "C180", "C180"),
            ("C182", "C182", "C182 Skylane"),
            // Cessna Caravan
            ("C208", "C208", "C208 Caravan"),
            // Cessna Citation jet family
            ("C25A", "C25A", "Citation CJ2"),
            ("C25B", "C25B", "Citation CJ3"),
            ("C25C", "C25C", "Citation CJ4"),
            ("C525", "C525", "Citation CJ1"),
            ("C550", "C550", "Citation II"),
            ("C560", "C560", "Citation V"),
            ("C680", "C680", "Citation Sovereign"),
            ("C700", "C700", "Citation Longitude"),
            // Piper family
            ("PA28", "PA28", "Piper Cherokee"),
            ("PA34", "PA34", "Piper Seneca"),
            ("PA44", "PA44", "Piper Seminole"),
            ("PA46", "PA46", "Piper Malibu"),
            // Embraer E-jets
            ("E170", "E170", "Embraer 170"),
            ("E175", "E175", "Embraer 175"),
            ("E190", "E190", "Embraer 190"),
            ("E195", "E195", "Embraer 195"),
            // Embraer business jets
            ("E35L", "E35L", "Legacy 600"),
            ("E50P", "E50P", "Phenom 100"),
            ("E55P", "E55P", "Phenom 300"),
            // Bombardier CRJ family
            ("CRJ2", "CRJ2", "CRJ-200"),
            ("CRJ7", "CRJ7", "CRJ-700"),
            ("CRJ9", "CRJ9", "CRJ-900"),
            ("CRJX", "CRJX", "CRJ-1000"),
            // Bombardier / Airbus A220
            ("BCS1", "BCS1", "A220-100"),
            ("BCS3", "BCS3", "A220-300"),
            // Bombardier Global
            ("GL5T", "GL5T", "Global 5000"),
            ("GLEX", "GLEX", "Global Express"),
            // Learjet
            ("LJ35", "LJ35", "Learjet 35"),
            ("LJ60", "LJ60", "Learjet 60"),
            ("LJ75", "LJ75", "Learjet 75"),
            // ATR family
            ("AT42", "AT42", "ATR 42-300"),
            ("AT43", "AT43", "ATR 42-300"),
            ("AT45", "AT45", "ATR 42-500"),
            ("AT72", "AT72", "ATR 72-200"),
            ("AT75", "AT75", "ATR 72-500"),
            ("AT76", "AT76", "ATR 72-600"),
            // De Havilland
            ("DH8A", "DH8A", "Dash 8-100"),
            ("DH8C", "DH8C", "Dash 8-300"),
            ("DH8D", "DH8D", "Dash 8-400"),
            ("DHC6", "DHC6", "Twin Otter"),
            // McDonnell Douglas
            ("MD11", "MD11", "MD-11"),
            ("MD82", "MD82", "MD-82"),
            ("MD83", "MD83", "MD-83"),
            // Daher TBM
            ("TBM7", "TBM7", "TBM 700"),
            ("TBM8", "TBM8", "TBM 850"),
            ("TBM9", "TBM9", "TBM 930"),
            // Cirrus
            ("SR20", "SR20", "SR20"),
            ("SR22", "SR22", "SR22"),
            ("SR2T", "SR2T", "SR22T"),
            // Beechcraft
            ("BE36", "BE36", "Bonanza G36"),
            ("BE58", "BE58", "Baron 58"),
            ("BE60", "BE60", "Duke"),
            ("B350", "B350", "King Air 350"),
            ("B06T", "B06T", "King Air C90"),
            // Pilatus
            ("PC12", "PC12", "PC-12"),
            ("PC24", "PC24", "PC-24"),
            // Fokker
            ("F100", "F100", "Fokker 100"),
            ("F28", "F28", "Fokker 28"),
            // HondaJet
            ("HDJT", "HDJT", "HondaJet"),
            // Light / Electric / Sport
            ("PIVI", "PIVI", "Pipistrel Velis"),
            ("ICON", "ICON", "ICON A5"),
            ("DRCO", "DRCO", "Robin DR400"),
            // Military
            ("F18H", "F18H", "F/A-18E Super Hornet"),
            ("F16C", "F16C", "F-16C Fighting Falcon"),
            ("A10", "A10", "A-10 Warthog"),
            ("B52", "B52", "B-52 Stratofortress"),
            ("C130", "C130", "C-130 Hercules")
        };

        public static void ResolveManufacturerAndModel(Aircraft aircraft)
        {
            if (aircraft == null) return;

            if (!string.IsNullOrWhiteSpace(aircraft.Manufacturer) && aircraft.Manufacturer.Length > 10)
            {
                var m = aircraft.Manufacturer.ToUpperInvariant();
                foreach (var (key, canonical) in ManufacturerMappings)
                {
                    if (m.Contains(key))
                    {
                        aircraft.Manufacturer = canonical;
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(aircraft.AircraftType) && aircraft.AircraftType.Length > 10)
            {
                var t = aircraft.AircraftType.ToUpperInvariant();
                foreach (var (key, type, model) in TypeMappings)
                {
                    if (t.Contains(key))
                    {
                        aircraft.AircraftType = type;
                        aircraft.Model = model;
                        break;
                    }
                }
            }
        }

        public static (string, double) GetAircraftIcon(Aircraft aircraft)
        {
            if (aircraft.Category.Equals("Helicopter"))
                return ("Helicopter", 0.6);
            (var aicraftIcon, var scaleFactor) = GetAircraftIcon(aircraft.AircraftType, true);
            if (aicraftIcon == null)
            {
                // If not matched on the type, try other heuristics
                if (aircraft.NumberOfEngines == 1 && aircraft.EngineType == EngineType.Piston)
                {
                    return ("C172", 0.6);
                }

                if (aircraft.NumberOfEngines == 2 && aircraft.EngineType == EngineType.Piston)
                {
                    return ("B200", 0.75);
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
                return ("ERJ", 0.65);

            if (B200.Any(aircraftType.Contains))
                return ("B200", 0.75);

            if (DC3.Any(aircraftType.Contains))
                return ("DC3", 0.65);

            if (Helicopter.Any(aircraftType.Contains))
                return ("Helicopter", 0.6);

            if (A400.Any(aircraftType.Contains))
                return ("A400", 0.75);

            if (Conc.Any(aircraftType.Contains))
                return ("Conc", 1.3);

            if (F16.Any(aircraftType.Contains))
                return ("F16", 0.75);

            if (F35.Any(aircraftType.Contains))
                return ("F35", 0.75);

            if (C208.Any(aircraftType.Contains))
                return ("C208", 0.75);

            if (isNullIfNotFound)
                return (null, 1);
            
            return ("B737", 0.75);
        }
    }
}
