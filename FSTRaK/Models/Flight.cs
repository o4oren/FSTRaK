using System;
using System.Collections.Generic;

namespace FSTRaK.Models
{
    internal class Flight
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Aircraft Aircraft { get; set; }
        public String DepartureAirport { get; set; }
        public String ArrivalAirport { get; set; }

        public List<double[]> FlightPath;


    }
}
