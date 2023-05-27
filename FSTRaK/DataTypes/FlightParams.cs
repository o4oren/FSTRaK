using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.DataTypes
{
    public struct FlightParams
    {
        public double Heading;
        public double Latitude;
        public double Longitude;
        public double IndicatedAirspeed;
        public double GroundSpeed;
        public double VeticalSpeed;
        public double Altitude;
        public bool IsOnGround;
    }
}
