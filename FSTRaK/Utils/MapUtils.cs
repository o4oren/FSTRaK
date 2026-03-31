using MapControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class MapUtils
    {
        /// <summary>
        /// Unwraps longitude values so they are continuous (no jumps greater than 180°).
        /// MapControl supports longitudes outside [-180, 180], so a trans-dateline polyline
        /// rendered with unwrapped coordinates draws correctly across the Pacific.
        /// </summary>
        public static IEnumerable<Location> WrapPolyline(IEnumerable<Location> input)
        {
            var output = new List<Location>();
            double offset = 0;
            double? prevRawLon = null;

            foreach (var point in input)
            {
                if (prevRawLon.HasValue)
                {
                    double delta = point.Longitude - prevRawLon.Value;
                    if (delta > 180)  offset -= 360;
                    else if (delta < -180) offset += 360;
                }

                output.Add(new Location(point.Latitude, point.Longitude + offset));
                prevRawLon = point.Longitude;
            }

            return output;
        }

        public static Location NormalizePoint(Location point, Location? prevPoint)
        {
            if (prevPoint != null)
            {
                double deltaLon = point.Longitude - prevPoint.Longitude;

                // If jump is greater than 180 degrees, we assume it crossed the dateline
                if (Math.Abs(deltaLon) > 180)
                {
                    // Determine direction of wrapping
                    double wrapLon = deltaLon > 0 ? 360 : -360;

                    // Insert interpolated point on other side of dateline
                    var interpolated = new Location(
                        (point.Latitude + prevPoint.Latitude) / 2,
                        prevPoint.Longitude + wrapLon / 2
                    );

                    return interpolated;
                }
            }
            return point;
        }

        public static IEnumerable<Location> WrapPolygon(IEnumerable<Location> input)
        {
            var output = new List<Location>();
            Location? prev = null;

            foreach (var point in input)
            {
                if (prev != null)
                {
                    double deltaLon = point.Longitude - prev.Longitude;

                    if (Math.Abs(deltaLon) > 180)
                    {
                        double wrapLon = deltaLon > 0 ? -360 : 360;

                        // Interpolate to simulate crossing the dateline
                        var interpolated1 = new Location(
                            prev.Latitude,
                            prev.Longitude + wrapLon
                        );

                        var interpolated2 = new Location(
                            point.Latitude,
                            point.Longitude + wrapLon
                        );

                        output.Add(interpolated1);
                        output.Add(interpolated2);
                    }
                    else
                    {
                        output.Add(point);
                    }
                }
                else
                {
                    output.Add(point);
                }

                prev = point;
            }

            return output;
        }
    }
}
