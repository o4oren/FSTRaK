using FSTRaK.Models;
using FSTRaK.Models.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal static class FlightExtentionMethods
    {
        public static Airport DepartureAirportDetails(this Flight flight)
        {
            var airport = AirportResolver.Instance.AirportsDictionary[flight.DepartureAirport];
            if (airport == null)
                return airport;
            return new Airport
            {
                icao = flight.DepartureAirport
            };
        }

        public static Airport ArrivalAirportDetails(this Flight flight)
        {
            var airport = AirportResolver.Instance.AirportsDictionary[flight.ArrivalAirport];
            if (airport == null)
                return airport;
            return new Airport
            {
                icao = flight.ArrivalAirport
            };
        }

    }
}
