namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoPilot
    {
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoLastTrack lastTrack { get; set; }
        public IvaoFlightPlan flightPlan { get; set; }
    }
}
