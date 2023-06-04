
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
    }
}
