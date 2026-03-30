using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoSubcenter
    {
        public string centerId { get; set; }
        public string atcCallsign { get; set; }
        public string position { get; set; }
        public double frequency { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public List<IvaoLatLng> regionMap { get; set; }
    }
}
