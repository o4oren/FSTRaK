using CsvHelper;
using CsvHelper.Configuration;
using FSTRaK.Models.Entity;
using Serilog;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FSTRaK.Models
{
    internal class AirportResolver
    {
        private static readonly object Lock = new object();
        private static AirportResolver _instance = null;

        public Dictionary<string, Airport> AirportsDictionary;

        private AirportResolver() 
        {
            LoadAirportsJson();
        }

        private void LoadAirportsJson()
        {
            var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location; // This was needed because using the resource directly caused it to point to c:\windows\system32 when starting with windows.
            var strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);

            if (strWorkPath != null)
            {
                var airportsCsvPath = Path.Combine(strWorkPath, "Resources", "Data", "airports.csv");
                Log.Information(airportsCsvPath);

                AirportsDictionary =
                    ReadCsvAsDictionary(airportsCsvPath);
            }

            Log.Information($"{AirportsDictionary.Count} airports loaded.");
        }

        public Airport GetAirportByIdentCode(string code)
        {
            try
            {
                return AirportsDictionary[code];
            }
            catch
            {
                return new Airport()
                {
                    icao_code = code,
                    ident = code
                };
            }
        }

        public Dictionary<string, Airport> ReadCsvAsDictionary(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            var airports = csv.GetRecords<Airport>()
              .ToDictionary(a => a.ident); ;

            return airports;
        }


        public static AirportResolver Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new AirportResolver();
                    }
                    return _instance;
                }
            }
        }
    }
}
