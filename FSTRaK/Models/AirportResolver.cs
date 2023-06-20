using FSTRaK.Models.Entity;
using Serilog;
using System.Collections.Generic;
using System.IO;
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

        private async void LoadAirportsJson()
        {
            var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location; // This was needed because using the resource directly caused it to point to c:\windows\system32 when starting with windows.
            var strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);

            if (strWorkPath != null)
            {
                var airportsJsonPath = Path.Combine(strWorkPath, "Resources", "Data", "airports.json");
                Log.Information(airportsJsonPath);

                var openStream = File.OpenRead(airportsJsonPath);
                AirportsDictionary =
                    await JsonSerializer.DeserializeAsync<Dictionary<string, Airport>>(openStream);
                openStream.Close();
            }

            Log.Information($"{AirportsDictionary.Count} airports loaded.");
        }

        public Airport GetAirportByIcaoCode(string code)
        {
            try
            {
                return AirportsDictionary[code];
            }
            catch
            {
                return new Airport()
                {
                    icao = code
                };
            }
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
