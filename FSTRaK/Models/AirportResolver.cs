using FSTRaK.Models.Entity;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class AirportResolver
    {
        private static readonly object _lock = new object();
        private static AirportResolver instance = null;

        public Dictionary<string, Airport> AirportsDictionary;

        private AirportResolver() 
        {
            LoadAirportsJson();
        }

        private async void LoadAirportsJson()
        {
            FileStream openStream = File.OpenRead(@".\Resources\\Data\\airports.json");
            AirportsDictionary =
                await JsonSerializer.DeserializeAsync<Dictionary<string, Airport>>(openStream);
            openStream.Close();
            Log.Information($"{AirportsDictionary.Count} airports loaded.");
        }

        public static AirportResolver Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new AirportResolver();
                    }
                    return instance;
                }
            }
        }
    }
}
