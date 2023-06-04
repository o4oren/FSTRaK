
using System.Globalization;
using System.Linq;
using System.Text;

namespace FSTRaK.Models.Entity
{
    internal class Airport
    {
        public string icao { set; get; }
        public string iata { set; get; }
        public string name { set; get; }
        public string city { set; get; }
        public string state { set; get; }
        public string country { set; get; }

        public int elevation { set; get; }
        public double lat { set; get; }
        public double lon { set; get; }
        public string tz { set; get; }

        public string CountryName {  
            get
            {
                var cultureInfo = new CultureInfo(country.ToLower());
                var ri = new RegionInfo(cultureInfo.Name);
                return ri.EnglishName;
            }
        }

        public override string ToString()
        {
            if(name == string.Empty || name == null) {
                return icao;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(city);
            if (country == "US")
                sb.Append(", ").Append($"{state}, USA");
            else sb.Append(", ").Append(CountryName);
            sb.Append($" ({icao})");
            return sb.ToString();
        }
    }
}
