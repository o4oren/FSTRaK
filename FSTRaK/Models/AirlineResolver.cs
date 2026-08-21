using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FSTRaK.Models
{
    /// <summary>
    /// Resolves ICAO airline designators (e.g. BAW) to airline names ("British Airways")
    /// from the bundled airlines.csv (derived from the OpenFlights airlines database).
    /// A missing or unreadable file leaves an empty dictionary — resolution then returns null.
    /// </summary>
    internal class AirlineResolver
    {
        private static readonly object Lock = new object();
        private static AirlineResolver _instance = null;

        private Dictionary<string, string> _airlineNamesByIcao = new Dictionary<string, string>();

        private AirlineResolver()
        {
            LoadAirlinesCsv();
        }

        private void LoadAirlinesCsv()
        {
            try
            {
                var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var strWorkPath = Path.GetDirectoryName(strExeFilePath);
                if (strWorkPath == null) return;

                var airlinesCsvPath = Path.Combine(strWorkPath, "Resources", "Data", "airlines.csv");
                _airlineNamesByIcao = ReadCsvAsDictionary(airlinesCsvPath);
                Log.Information($"{_airlineNamesByIcao.Count} airlines loaded.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load airlines.csv - airline ICAO codes will not be resolved to names");
            }
        }

        /// <summary>
        /// Returns the airline name for an ICAO designator, or null when unknown.
        /// </summary>
        public string GetAirlineNameByIcao(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            return _airlineNamesByIcao.TryGetValue(icao.Trim().ToUpperInvariant(), out var name) ? name : null;
        }

        internal static Dictionary<string, string> ReadCsvAsDictionary(string filePath)
        {
            using var reader = new StreamReader(filePath);
            return ReadCsvAsDictionary(reader);
        }

        internal static Dictionary<string, string> ReadCsvAsDictionary(TextReader reader)
        {
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            var airlines = new Dictionary<string, string>();
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var icao = csv.GetField("icao");
                var name = csv.GetField("name");
                if (!string.IsNullOrWhiteSpace(icao) && !string.IsNullOrWhiteSpace(name))
                    airlines[icao.Trim().ToUpperInvariant()] = name.Trim();
            }
            return airlines;
        }

        public static AirlineResolver Instance
        {
            get
            {
                lock (Lock)
                {
                    return _instance ??= new AirlineResolver();
                }
            }
        }
    }
}
