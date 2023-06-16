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
            var openStream = File.OpenRead(@".\Resources\\Data\\airports.json");
            AirportsDictionary =
                await JsonSerializer.DeserializeAsync<Dictionary<string, Airport>>(openStream);
            openStream.Close();
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
