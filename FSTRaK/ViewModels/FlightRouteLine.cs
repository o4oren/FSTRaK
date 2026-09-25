using MapControl;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// One flown route on the statistics map, with its hover text already built.
    ///
    /// The text is composed while the logbook context is still open rather than holding on
    /// to the Flight: the statistics query runs on a background task whose context is
    /// disposed as soon as it completes, so anything the map needs later has to be a plain
    /// value by then.
    /// </summary>
    internal sealed class FlightRouteLine
    {
        public Location Departure { get; }
        public Location Arrival { get; }
        public string TooltipText { get; }

        public FlightRouteLine(Location departure, Location arrival, string tooltipText)
        {
            Departure = departure;
            Arrival = arrival;
            TooltipText = tooltipText;
        }
    }
}
