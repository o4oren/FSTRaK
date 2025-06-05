
using System.Globalization;
using System.Linq;
using System.Text;

namespace FSTRaK.Models.Entity
{
    public class Airport
    {
        // "id","ident","type","name","latitude_deg","longitude_deg","elevation_ft","continent","iso_country","iso_region","municipality","scheduled_service","icao_code","iata_code","gps_code","local_code","home_link","wikipedia_link","keywords"
        public string ident {  get; set; }
        public string name { set; get; }
        public string type { get; set; }
        public double latitude_deg { set; get; }
        public double longitude_deg { set; get; }
        public int? elevation_ft { set; get; }

        public string continent { set; get; }
        public string iso_country { set; get; }
        public string iso_region { set; get; }
        public string municipality { set; get; }

        public string scheduled_service { set; get; }

        public string icao_code { set; get; }
        public string iata_code { set; get; }
        public string local_code { set; get; }

        public string home_link { set; get; }
        public string wikipedia_link { set; get; }

        public string CountryName {  
            get
            {
                var cultureInfo = new CultureInfo("en-US");
                var ri = new RegionInfo(cultureInfo.Name);
                return ri.EnglishName;
            }
        }

        public override string ToString()
        {
            if(string.IsNullOrEmpty(name)) {
                return ident;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(municipality);
            if (iso_country == "US")
                sb.Append(", ").Append($"{iso_region.Split('-')[1]}, USA");
            else sb.Append(", ").Append(CountryName);
            sb.Append($" ({ident})");
            return sb.ToString();
        }
    }
}
