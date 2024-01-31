namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    public class Pilot
    {
        public int altitude { get; set; }
        public string callsign { get; set; }
        public int cid { get; set; }
        public int groundspeed { get; set; }
        public FlightPlan flight_plan { get; set; }
        public int heading { get; set; }
        public string last_updated { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string name { get; set; }

    }
}
