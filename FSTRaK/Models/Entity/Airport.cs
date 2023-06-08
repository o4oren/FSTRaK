
using System.Globalization;
using System.Linq;
using System.Text;

namespace FSTRaK.Models.Entity
{
    internal class Airport
    {
        public string Icao { set; get; }
        public string Iata { set; get; }
        public string Name { set; get; }
        public string City { set; get; }
        public string State { set; get; }
        public string Country { set; get; }

        public int Elevation { set; get; }
        public double Lat { set; get; }
        public double Lon { set; get; }
        public string Tz { set; get; }

        public string CountryName {  
            get
            {
                var cultureInfo = new CultureInfo(Country.ToLower());
                var ri = new RegionInfo(cultureInfo.Name);
                return ri.EnglishName;
            }
        }

        public override string ToString()
        {
            if(string.IsNullOrEmpty(Name)) {
                return Icao;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(City);
            if (Country == "US")
                sb.Append(", ").Append($"{State}, USA");
            else sb.Append(", ").Append(CountryName);
            sb.Append($" ({Icao})");
            return sb.ToString();
        }
    }
}
