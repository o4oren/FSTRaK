namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoLastTrack
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
        public int altitude { get; set; }
        public int heading { get; set; }
        public int groundSpeed { get; set; }
        public bool onGround { get; set; }
        public string state { get; set; }
    }
}
