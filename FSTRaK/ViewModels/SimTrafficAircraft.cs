using System;
using System.Linq;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;
using MapControl;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// One simulator traffic object as the map draws it. Built fresh from each snapshot
    /// rather than mutated, which is why it carries no change notification.
    /// </summary>
    internal sealed class SimTrafficAircraft
    {
        public uint ObjectId { get; }
        public string Callsign { get; }

        /// <summary>The ICAO-ish type code, from ATC Model - this is what resolves the icon.</summary>
        public string AircraftType { get; }

        /// <summary>From ATC Type, which despite its name is the manufacturer.</summary>
        public string Manufacturer { get; }
        public string Category { get; }
        public string Airline { get; }
        public Location Location { get; }
        public double Heading { get; }
        public int Altitude { get; }
        public int Groundspeed { get; }
        public bool IsOnGround { get; }
        public string IconResource { get; }
        public double ScaleFactor { get; }

        public SimTrafficAircraft(SimTrafficEntry entry)
        {
            var data = entry.Data;

            ObjectId = entry.ObjectId;

            // AI objects report these as localisation keys rather than resolved text.
            AircraftType = SimVarText.Humanize((data.AtcModel ?? string.Empty).Trim());
            Manufacturer = SimVarText.Humanize((data.AtcType ?? string.Empty).Trim());
            Airline = SimVarText.Humanize((data.Airline ?? string.Empty).Trim());
            Category = (data.Category ?? string.Empty).Trim();
            Callsign = ResolveCallsign(data.AtcId, data.FlightNumber, data.Title);
            Location = new Location(data.Latitude, data.Longitude);
            Heading = data.TrueHeading;
            Altitude = (int)Math.Round(data.Altitude);
            Groundspeed = (int)Math.Round(data.GroundVelocity);
            IsOnGround = data.SimOnGround != 0;

            var (icon, scale) = AircraftResolver.GetAircraftIcon(Category, AircraftType, data.NumberOfEngines, data.EngineType);
            IconResource = icon;
            ScaleFactor = scale;
        }

        /// <summary>
        /// AI objects are inconsistently labelled: sim traffic usually carries an ATC ID,
        /// live traffic often only a flight number, and multiplayer aircraft sometimes
        /// neither. The title is the last resort so the map never shows a blank label.
        /// </summary>
        private static string ResolveCallsign(string atcId, string flightNumber, string title)
        {
            var id = (atcId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            var number = (flightNumber ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(number))
            {
                return number;
            }

            return SimVarText.Humanize((title ?? string.Empty).Trim());
        }

        public string TooltipText => CreateTooltipText();

        private string CreateTooltipText()
        {
            var airline = string.IsNullOrEmpty(Airline) ? "" : $"{Airline}\n";
            var aircraft = string.Join(" ", new[] { Manufacturer, AircraftType }
                .Where(part => !string.IsNullOrEmpty(part)));
            var state = IsOnGround ? "ON GROUND" : $"ALT: {Altitude:N0}";
            return $"{Callsign}\n{airline}{aircraft}\n{state}  GS: {Groundspeed}  HDG: {Heading:F0}";
        }
    }
}
