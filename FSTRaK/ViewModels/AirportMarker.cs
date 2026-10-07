using MapControl;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// One airport dot on the statistics route map, carrying the details shown on hover.
    /// </summary>
    internal sealed class AirportMarker
    {
        public string Icao { get; }
        public Location Position { get; }
        public string Name { get; }
        public string City { get; }
        public string Country { get; }

        public AirportMarker(string icao, Location position, string name, string city, string country)
        {
            Icao = icao;
            Position = position;
            Name = name;
            City = city;
            Country = country;
        }
    }
}
