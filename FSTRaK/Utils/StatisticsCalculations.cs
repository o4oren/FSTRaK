using System;
using System.Collections.Generic;
using System.Linq;
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.Utils
{
    /// <summary>
    /// Pure data aggregations behind the statistics view. Kept free of chart/UI types
    /// so they can be unit tested.
    /// </summary>
    internal static class StatisticsCalculations
    {
        // LandingFpm is -1 by default (flight never recorded a landing) and null for
        // flights persisted before the column existed. Neither is a real landing.
        private const double LandingFpmSentinel = -1;

        public static bool HasValidLandingFpm(Flight flight)
        {
            return flight.LandingFpm.HasValue && flight.LandingFpm.Value != LandingFpmSentinel;
        }

        public static double? AverageLandingFpm(List<Flight> flights)
        {
            var valid = flights.Where(HasValidLandingFpm).ToList();
            return valid.Any() ? valid.Average(f => f.LandingFpm) : null;
        }

        public static Dictionary<DateTime, double> CalculateFlightsPerDay(List<Flight> flights)
        {
            return flights
                .GroupBy(f => f.StartTime.Date)
                .ToDictionary(g => g.Key, g => Convert.ToDouble(g.Count()));
        }

        /// <summary>
        /// Aggregates the per-day counts into the requested period and fills every period
        /// between the first and last flight with a zero bucket, so the chart shows a
        /// continuous timeline instead of collapsing empty stretches.
        /// </summary>
        public static List<KeyValuePair<DateTime, double>> AggregateByPeriod(
            Dictionary<DateTime, double> flightsPerDay, TimePeriod period)
        {
            if (flightsPerDay.Count == 0)
                return new List<KeyValuePair<DateTime, double>>();

            var grouped = flightsPerDay
                .GroupBy(kv => TruncateToPeriod(kv.Key, period))
                .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

            var first = grouped.Keys.Min();
            var last = grouped.Keys.Max();

            var result = new List<KeyValuePair<DateTime, double>>();
            for (var bucket = first; bucket <= last; bucket = NextPeriod(bucket, period))
            {
                grouped.TryGetValue(bucket, out var count);
                result.Add(new KeyValuePair<DateTime, double>(bucket, count));
            }
            return result;
        }

        private static DateTime TruncateToPeriod(DateTime date, TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Month:
                    return new DateTime(date.Year, date.Month, 1);
                case TimePeriod.Year:
                    return new DateTime(date.Year, 1, 1);
                default:
                    return date.Date;
            }
        }

        private static DateTime NextPeriod(DateTime bucket, TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Month:
                    return bucket.AddMonths(1);
                case TimePeriod.Year:
                    return bucket.AddYears(1);
                default:
                    return bucket.AddDays(1);
            }
        }

        public static List<(double bucketCenter, int count)> CalculateLandingRateDistribution(List<Flight> flights)
        {
            const int bucketSize = 50;
            const int minFpm = -1000;
            const int maxFpm = 0;

            var buckets = new Dictionary<int, int>();
            for (int b = minFpm; b < maxFpm; b += bucketSize)
                buckets[b] = 0;

            foreach (var f in flights.Where(HasValidLandingFpm))
            {
                var fpm = (int)f.LandingFpm.Value;
                var bucket = (int)(Math.Floor((double)fpm / bucketSize) * bucketSize);
                bucket = Math.Max(minFpm, Math.Min(maxFpm - bucketSize, bucket));
                if (buckets.ContainsKey(bucket))
                    buckets[bucket]++;
            }

            return buckets
                .OrderBy(kv => kv.Key)
                .Select(kv => (bucketCenter: (double)(kv.Key + bucketSize / 2), count: kv.Value))
                .ToList();
        }
    }
}
