using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    internal class VatsimStaticData
    {
        public List<VatsimStaticData.Airport> Airports { get; set; }
        public List<VatsimStaticData.FIR> FIRs { get; set; }
        public Dictionary<string, VatsimStaticData.Country> Countries { get; set; }


        public VatsimStaticData()
        {
            Countries = new Dictionary<string, Country>();
            Airports = new List<Airport>();
            FIRs = new List<FIR>();
        }


        public class Country
        {
            //name, initials, centerName
            public string Name { get; set; }
            public string Initials { get; set; }
            public string centerName { get; set; }
        }

        public class Airport
        {
            //ICAO|Airport Name|Latitude Decimal|Longitude Decimal|IATA/LID|FIR|IsPseudo
            public string ICAO { get; set; }
            public string Name { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string IATA { get; set; }
            public string FIR { get; set; }
            public bool IsPseudo { get; set; }
        }

        public class FIR
        {
            //ICAO|NAME|CALLSIGN PREFIX|FIR BOUNDARY
            public string ICAO { get; set; }
            public string Name { get; set; }
            public string CallsignPrefix { get; set; }
            public string Boundary { get; set; }
        }

        public class UIR
        {
            //Callsign prefix|NAME|FIRs
            public string CallsignPrefix { get; set; }
            public string Name { get; set; }
            public List<string> Firs { get; set; }
        }

        class IDL
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }

        }

; 
    }
}
