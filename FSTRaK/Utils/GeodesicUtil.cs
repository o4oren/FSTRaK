using System;
using System.Collections.Generic;
using MapControl;

namespace FSTRaK.Utils
{
    public static class GeodesicUtil
    {
        private const double EarthRadiusNm = 3440.065;

        /// <summary>
        /// Returns a series of lat/lon points along the great-circle path from
        /// (startLat, startLon) to (endLat, endLon), with approximately one point
        /// every <paramref name="stepNm"/> nautical miles.
        /// </summary>
        public static List<Location> Interpolate(
            double startLat, double startLon,
            double endLat, double endLon,
            double stepNm = 50.0)
        {
            var points = new List<Location>();
            double lat1 = ToRad(startLat), lon1 = ToRad(startLon);
            double lat2 = ToRad(endLat),  lon2 = ToRad(endLon);

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double centralAngle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double sinD = Math.Sin(centralAngle);

            // Degenerate case: start and end are the same point or antipodal
            if (Math.Abs(sinD) < 1e-10)
            {
                points.Add(new Location(startLat, startLon));
                return points;
            }

            double totalNm = centralAngle * EarthRadiusNm;
            int steps = Math.Max(2, (int)(totalNm / stepNm));
            for (int i = 0; i <= steps; i++)
            {
                double f = (double)i / steps;
                double A = Math.Sin((1 - f) * centralAngle) / sinD;
                double B = Math.Sin(f * centralAngle) / sinD;
                double x = A * Math.Cos(lat1) * Math.Cos(lon1) + B * Math.Cos(lat2) * Math.Cos(lon2);
                double y = A * Math.Cos(lat1) * Math.Sin(lon1) + B * Math.Cos(lat2) * Math.Sin(lon2);
                double z = A * Math.Sin(lat1) + B * Math.Sin(lat2);
                double lat = Math.Atan2(z, Math.Sqrt(x * x + y * y));
                double lon = Math.Atan2(y, x);
                points.Add(new Location(ToDeg(lat), ToDeg(lon)));
            }
            return points;
        }

        /// <summary>Distance in nautical miles between two lat/lon points.</summary>
        public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            double r1 = ToRad(lat1), r2 = ToRad(lat2);
            double dLat = r2 - r1;
            double dLon = ToRad(lon2) - ToRad(lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(r1) * Math.Cos(r2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)) * EarthRadiusNm;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
        private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
