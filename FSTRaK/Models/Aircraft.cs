using System;

namespace FSTRaK.Models
{
    internal class Aircraft : BaseModel
    {
        public string Title { get; set; }
        public String Manufacturer { get; set; }
        public String Code { get; set; }
        public String Airline { get; set; }
        public String Model { get; set; }
        public String TailNumber { get; set; }

        public string Details { get
            {
                return $"Lat: {Position[0]:F4} Lon:{Position[1]:F4} \nHeading: {Heading:F0} Alt: {Altitude:F0} ft\nSpeed: {Airspeed:F0} Knots";
            }
            set { }
        }

        public double Heading { get; set; }
        public double Altitude { get; set; }
        public double Airspeed { get; set; }


        public double[] Position { get; set; } = { 0, 0 };



        public override bool Equals(Object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                return ((Aircraft)obj).Title == this.Title;
            }
        }
    }
}
