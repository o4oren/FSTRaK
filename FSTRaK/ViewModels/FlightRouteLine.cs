using System.Collections.Generic;
using MapControl;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// One flight entry in a route's hover list.
    /// </summary>
    internal sealed class FlightRouteLegend
    {
        public string Date { get; }
        public string Airline { get; }
        public string Aircraft { get; }
        public string Direction { get; }

        public FlightRouteLegend(string date, string airline, string aircraft, string direction)
        {
            Date = date;
            Airline = airline;
            Aircraft = aircraft;
            Direction = direction;
        }
    }

    /// <summary>
    /// One drawn route on the statistics map, merging both directions between the same two
    /// airports (A→B and B→A) into a single line, since they occupy the same physical track.
    /// Carries every flight that flew this airport pair, so a popular route's hover shows all
    /// of them rather than just one.
    ///
    /// Built while the flights and their eagerly-loaded aircraft are still in scope: the
    /// statistics query runs on a background task whose context is disposed as soon as it
    /// completes, so anything the map needs later has to be a plain value by then.
    /// </summary>
    internal sealed class FlightRouteLine
    {
        public Location Departure { get; }
        public Location Arrival { get; }
        public IReadOnlyList<FlightRouteLegend> Flights { get; }

        public FlightRouteLine(Location departure, Location arrival, IReadOnlyList<FlightRouteLegend> flights)
        {
            Departure = departure;
            Arrival = arrival;
            Flights = flights;
        }
    }
}
