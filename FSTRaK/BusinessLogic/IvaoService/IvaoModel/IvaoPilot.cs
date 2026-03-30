namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoPilot
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public long SessionId { get; set; }  // session ID — for tracks API (requires auth)
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoLastTrack lastTrack { get; set; }
        public IvaoFlightPlan flightPlan { get; set; }
    }
}
