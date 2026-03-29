namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcEntry
    {
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoAtcSessionInfo atcSession { get; set; }
        public IvaoAtcPositionInfo atcPosition { get; set; }
        public IvaoSubcenter subcenter { get; set; }
    }
}
