using System;

namespace FSTRaK.Models
{
    internal class FlightEvent : BaseModel
    {

        public int ID { get; set; }
        public DateTime Time { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Airspeed { get; set; }
        public double GroundSpeed { get; set; }
        public double TrueHeading { get; set; }
        public double GroundAltitude { get; set; }
        public int FlightID { get; set; }

        public virtual Flight Flight { get; set; }
    }
}
