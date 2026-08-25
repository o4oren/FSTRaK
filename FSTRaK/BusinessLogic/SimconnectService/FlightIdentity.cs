using System;
using System.Device.Location;
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// The aircraft and position at one instant, used to decide whether a flight survived
    /// a connection gap.
    /// </summary>
    internal struct FlightIdentitySnapshot
    {
        public string Title;
        public string LiveryName;
        public double Latitude;
        public double Longitude;
        public bool OnGround;
    }

    /// <summary>
    /// Decides whether the session seen after a reconnect is the same flight that was
    /// under way before the connection dropped.
    ///
    /// The airborne tolerance is the looser of the two deliberately: restarting a flight,
    /// getting airborne and arriving within 30 nm of the last known position inside the
    /// grace window is implausible, whereas a 30 nm radius on the ground could easily
    /// span a neighbouring airport.
    /// </summary>
    internal static class FlightIdentity
    {
        private const double AirborneToleranceNauticalMiles = 30.0;
        private const double GroundToleranceNauticalMiles = 20.0;

        public static bool CanResume(
            FlightIdentitySnapshot before,
            FlightIdentitySnapshot after,
            bool isInFlight,
            bool isMsfs2024)
        {
            if (!isInFlight)
            {
                return false;
            }

            if (!TitlesMatch(before.Title, after.Title))
            {
                return false;
            }

            // Only MSFS 2024 reports a livery that distinguishes variants of one title.
            if (isMsfs2024 && !TitlesMatch(before.LiveryName, after.LiveryName))
            {
                return false;
            }

            var tolerance = after.OnGround
                ? GroundToleranceNauticalMiles
                : AirborneToleranceNauticalMiles;

            return DistanceInNauticalMiles(before, after) <= tolerance;
        }

        private static bool TitlesMatch(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static double DistanceInNauticalMiles(
            FlightIdentitySnapshot before,
            FlightIdentitySnapshot after)
        {
            var from = new GeoCoordinate(before.Latitude, before.Longitude);
            var to = new GeoCoordinate(after.Latitude, after.Longitude);
            return from.GetDistanceTo(to) * Consts.MetersToNauticalMiles;
        }
    }
}
