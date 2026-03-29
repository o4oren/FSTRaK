using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcPositionInfo
    {
        public string airportId { get; set; }
        public string atcCallsign { get; set; }
        public string position { get; set; }
        public List<IvaoLatLng> regionMap { get; set; }
        public IvaoAirport airport { get; set; }
    }

    public class IvaoLatLng
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class IvaoAirport
    {
        public string icao { get; set; }
        public string name { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
    }
}
