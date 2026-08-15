using System;
using System.Collections.Generic;
using System.Linq;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Utils;
using Xunit;

namespace FSTRaK.Tests
{
    public class StatisticsCalculationsTests
    {
        private static Flight FlightAt(DateTime startTime, double? landingFpm = null)
        {
            return new Flight { StartTime = startTime, LandingFpm = landingFpm };
        }

        // ── CalculateFlightsPerDay ────────────────────────────────────────────

        [Fact]
        public void CalculateFlightsPerDay_GroupsByDateIgnoringTimeOfDay()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 5, 8, 30, 0)),
                FlightAt(new DateTime(2026, 1, 5, 21, 15, 0)),
                FlightAt(new DateTime(2026, 1, 7, 12, 0, 0))
            };

            var result = StatisticsCalculations.CalculateFlightsPerDay(flights);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[new DateTime(2026, 1, 5)]);
            Assert.Equal(1, result[new DateTime(2026, 1, 7)]);
        }

        // ── AggregateByPeriod: gap filling ────────────────────────────────────

        [Fact]
        public void AggregateByPeriod_Day_FillsMissingDaysWithZero()
        {
            var perDay = new Dictionary<DateTime, double>
            {
                [new DateTime(2026, 1, 1)] = 2,
                [new DateTime(2026, 1, 4)] = 1
            };

            var result = StatisticsCalculations.AggregateByPeriod(perDay, TimePeriod.Day);

            Assert.Equal(4, result.Count);
            Assert.Equal(new DateTime(2026, 1, 1), result[0].Key);
            Assert.Equal(2, result[0].Value);
            Assert.Equal(0, result[1].Value);
            Assert.Equal(0, result[2].Value);
            Assert.Equal(new DateTime(2026, 1, 4), result[3].Key);
            Assert.Equal(1, result[3].Value);
        }

        [Fact]
        public void AggregateByPeriod_Month_SumsDaysAndFillsMissingMonths()
        {
            var perDay = new Dictionary<DateTime, double>
            {
                [new DateTime(2026, 1, 10)] = 2,
                [new DateTime(2026, 1, 20)] = 3,
                [new DateTime(2026, 4, 2)] = 1
            };

            var result = StatisticsCalculations.AggregateByPeriod(perDay, TimePeriod.Month);

            Assert.Equal(4, result.Count);
            Assert.Equal(new DateTime(2026, 1, 1), result[0].Key);
            Assert.Equal(5, result[0].Value);
            Assert.Equal(new DateTime(2026, 2, 1), result[1].Key);
            Assert.Equal(0, result[1].Value);
            Assert.Equal(new DateTime(2026, 3, 1), result[2].Key);
            Assert.Equal(0, result[2].Value);
            Assert.Equal(new DateTime(2026, 4, 1), result[3].Key);
            Assert.Equal(1, result[3].Value);
        }

        [Fact]
        public void AggregateByPeriod_Year_SumsAndFillsMissingYears()
        {
            var perDay = new Dictionary<DateTime, double>
            {
                [new DateTime(2023, 6, 1)] = 4,
                [new DateTime(2026, 2, 1)] = 2
            };

            var result = StatisticsCalculations.AggregateByPeriod(perDay, TimePeriod.Year);

            Assert.Equal(4, result.Count);
            Assert.Equal(new DateTime(2023, 1, 1), result[0].Key);
            Assert.Equal(4, result[0].Value);
            Assert.Equal(0, result[1].Value);
            Assert.Equal(0, result[2].Value);
            Assert.Equal(new DateTime(2026, 1, 1), result[3].Key);
            Assert.Equal(2, result[3].Value);
        }

        [Fact]
        public void AggregateByPeriod_SingleDay_ReturnsSingleEntry()
        {
            var perDay = new Dictionary<DateTime, double> { [new DateTime(2026, 5, 5)] = 3 };

            var result = StatisticsCalculations.AggregateByPeriod(perDay, TimePeriod.Day);

            Assert.Single(result);
            Assert.Equal(3, result[0].Value);
        }

        [Fact]
        public void AggregateByPeriod_Empty_ReturnsEmpty()
        {
            var result = StatisticsCalculations.AggregateByPeriod(new Dictionary<DateTime, double>(), TimePeriod.Month);

            Assert.Empty(result);
        }

        [Fact]
        public void AggregateByPeriod_ResultIsOrderedChronologically()
        {
            var perDay = new Dictionary<DateTime, double>
            {
                [new DateTime(2026, 3, 1)] = 1,
                [new DateTime(2026, 1, 1)] = 1,
                [new DateTime(2026, 2, 1)] = 1
            };

            var result = StatisticsCalculations.AggregateByPeriod(perDay, TimePeriod.Day);

            Assert.True(result.SequenceEqual(result.OrderBy(kv => kv.Key)));
        }

        // ── Landing FPM validity / average ────────────────────────────────────

        [Fact]
        public void AverageLandingFpm_ExcludesNullAndSentinel()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 1), -200),
                FlightAt(new DateTime(2026, 1, 2), -400),
                FlightAt(new DateTime(2026, 1, 3), null),   // legacy: no data
                FlightAt(new DateTime(2026, 1, 4), -1)      // default: never landed
            };

            var avg = StatisticsCalculations.AverageLandingFpm(flights);

            Assert.Equal(-300, avg);
        }

        [Fact]
        public void AverageLandingFpm_NoValidLandings_ReturnsNull()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 1), null),
                FlightAt(new DateTime(2026, 1, 2), -1)
            };

            Assert.Null(StatisticsCalculations.AverageLandingFpm(flights));
        }

        // ── CalculateLandingRateDistribution ──────────────────────────────────

        [Fact]
        public void CalculateLandingRateDistribution_BucketsLandingsByFpm()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 1), -125), // bucket -150..-100, center -125
                FlightAt(new DateTime(2026, 1, 2), -130), // same bucket
                FlightAt(new DateTime(2026, 1, 3), -600)  // bucket -600..-550, center -575
            };

            var dist = StatisticsCalculations.CalculateLandingRateDistribution(flights);

            Assert.Equal(20, dist.Count); // -1000..0 in 50 fpm buckets
            Assert.Equal(2, dist.Single(d => d.bucketCenter == -125).count);
            Assert.Equal(1, dist.Single(d => d.bucketCenter == -575).count);
            Assert.Equal(3, dist.Sum(d => d.count));
        }

        [Fact]
        public void CalculateLandingRateDistribution_ExcludesSentinelAndNull()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 1), -1),
                FlightAt(new DateTime(2026, 1, 2), null)
            };

            var dist = StatisticsCalculations.CalculateLandingRateDistribution(flights);

            Assert.Equal(0, dist.Sum(d => d.count));
        }

        [Fact]
        public void CalculateLandingRateDistribution_ClampsExtremeValuesIntoEdgeBuckets()
        {
            var flights = new List<Flight>
            {
                FlightAt(new DateTime(2026, 1, 1), -2500) // harder than -1000: clamps to lowest bucket
            };

            var dist = StatisticsCalculations.CalculateLandingRateDistribution(flights);

            Assert.Equal(1, dist.Single(d => d.bucketCenter == -975).count);
        }
    }
}
