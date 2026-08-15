using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity.FlightEvent;
using Xunit;

namespace FSTRaK.Tests
{
    public class FlightScoreTests
    {
        [Fact]
        public void UpdateScore_NoScoringEvents_Is100()
        {
            var flight = new Flight();

            flight.UpdateScore();

            Assert.Equal(100, flight.Score);
        }

        [Fact]
        public void UpdateScore_SumsDeltasAcrossEventTypes()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -600, LandingRate = LandingRate.Hard, ScoreDelta = -35 });
            flight.FlightEvents.Add(new OverspeedEvent { ScoreDelta = -10 });

            flight.UpdateScore();

            Assert.Equal(55, flight.Score);
        }

        [Fact]
        public void UpdateScore_CountsOnlyFirstEventOfEachType()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -600, ScoreDelta = -35 });
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -400, ScoreDelta = -10 });

            flight.UpdateScore();

            Assert.Equal(65, flight.Score);
        }

        [Fact]
        public void UpdateScore_ClampsAtZero()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new CrashEvent { ScoreDelta = -200 });

            flight.UpdateScore();

            Assert.Equal(0, flight.Score);
        }

        [Fact]
        public void UpdateScore_ClampsAt110()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -170, ScoreDelta = 10 });
            flight.FlightEvents.Add(new StallWarningEvent { ScoreDelta = 20 });

            flight.UpdateScore();

            Assert.Equal(110, flight.Score);
        }

        [Fact]
        public void UpdateScore_SetsLandingFpmFromLandingEvent()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -287.5, ScoreDelta = 0 });

            flight.UpdateScore();

            Assert.Equal(-287.5, flight.LandingFpm);
        }

        [Fact]
        public void UpdateScore_NoLandingEvent_LeavesLandingFpmDefault()
        {
            var flight = new Flight();

            flight.UpdateScore();

            Assert.Equal(-1, flight.LandingFpm);
        }

        [Fact]
        public void GetScoreDetails_LandingLine_ContainsRateFpmGForceAndPoints()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent
            {
                VerticalSpeed = -600,
                LandingRate = LandingRate.Hard,
                ScoreDelta = -35,
                TouchdownGForce = 1.62
            });

            var details = flight.GetScoreDetails();

            Assert.Contains("Hard", details);
            Assert.Contains("Landing", details);
            Assert.Contains($"{-600.0:F0} fpm", details);
            Assert.Contains($"{1.62:F2} G", details);
            Assert.Contains("-35 Points", details);
        }

        [Fact]
        public void GetScoreDetails_LandingWithoutGForce_OmitsGForce()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent
            {
                VerticalSpeed = -600,
                LandingRate = LandingRate.Hard,
                ScoreDelta = -35,
                TouchdownGForce = null
            });

            var details = flight.GetScoreDetails();

            Assert.Contains($"{-600.0:F0} fpm", details);
            Assert.DoesNotContain(" G)", details);
        }

        [Fact]
        public void GetScoreDetails_ZeroDeltaEvents_AreOmitted()
        {
            var flight = new Flight();
            flight.FlightEvents.Add(new LandingEvent { VerticalSpeed = -200, LandingRate = LandingRate.Good, ScoreDelta = 0 });

            var details = flight.GetScoreDetails();

            Assert.Equal(string.Empty, details);
        }
    }
}
