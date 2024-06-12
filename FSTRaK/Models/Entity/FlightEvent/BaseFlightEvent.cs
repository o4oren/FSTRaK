using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    [Table("FlightEvent")]
    public class BaseFlightEvent : BaseModel
    {
        [Column("ID")]
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double IndicatedAirspeed { get; set; }
        public double GroundSpeed { get; set; }
        public double TrueHeading { get; set; }
        public double GroundAltitude { get; set; }

        [Index(nameof(FlightId))]
        public int FlightId { get; set; }

        public virtual Flight Flight { get; set; }

        [NotMapped] public virtual string EventName { get; set; } = "Flight event";


        [NotMapped]
        public string Location 
        { 
            get => $"{Latitude},{Longitude}";
            private set { } 
        }

        public override string ToString()
        {
            return $"{EventName}\n{Time.ToShortTimeString()}";
        }
    }
}
