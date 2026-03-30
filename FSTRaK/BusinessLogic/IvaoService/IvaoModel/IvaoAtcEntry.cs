namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcEntry
    {
        public long id { get; set; }         // session ID — used for detail/atis API fetches
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoAtcSessionInfo atcSession { get; set; }
        public IvaoAtcPositionInfo atcPosition { get; set; }
        public IvaoSubcenter subcenter { get; set; }
    }
}
