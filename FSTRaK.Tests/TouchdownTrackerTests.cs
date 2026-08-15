using FSTRaK.BusinessLogic.FlightManager.State;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Xunit;

namespace FSTRaK.Tests
{
    public class TouchdownTrackerTests
    {
        private static TouchdownTracker CreateTracker(double verticalSpeed, double touchdownGForce, out LandingEvent landingEvent)
        {
            landingEvent = new LandingEvent { VerticalSpeed = verticalSpeed };
            TouchdownTracker.ApplyVerticalSpeedRating(landingEvent);
            var touchdownData = new FlightData { VerticalSpeed = verticalSpeed, GForce = touchdownGForce };
            return new TouchdownTracker(landingEvent, touchdownData);
        }

        [Theory]
        [InlineData(-600, LandingRate.Hard, -35)]
        [InlineData(-501, LandingRate.Hard, -35)]
        [InlineData(-500, LandingRate.Fair, -10)]
        [InlineData(-400, LandingRate.Fair, -10)]
        [InlineData(-350, LandingRate.Good, 0)]
        [InlineData(-200, LandingRate.Good, 0)]
        [InlineData(-190, LandingRate.Perfect, 10)]
        [InlineData(-170, LandingRate.Perfect, 10)]
        [InlineData(-165, LandingRate.Good, 0)]
        [InlineData(-150, LandingRate.Good, 0)]
        [InlineData(-135, LandingRate.Soft, -10)]
        [InlineData(-120, LandingRate.Soft, -10)]
        [InlineData(-101, LandingRate.Soft, 0)]
        [InlineData(-80, LandingRate.Soft, 0)]
        public void ApplyVerticalSpeedRating_SetsExpectedRatingAndDelta(double fpm, LandingRate expectedRate, int expectedDelta)
        {
            var landingEvent = new LandingEvent { VerticalSpeed = fpm };

            TouchdownTracker.ApplyVerticalSpeedRating(landingEvent);

            Assert.Equal(expectedRate, landingEvent.LandingRate);
            Assert.Equal(expectedDelta, landingEvent.ScoreDelta);
        }

        [Fact]
        public void FinalizeLanding_GWorseThanFpm_TakesGRating()
        {
            // -200 fpm is Good (0 points), 1.6 G is Hard (-35 points) — the G delta wins.
            var tracker = CreateTracker(-200, 1.6, out var landingEvent);

            tracker.FinalizeLanding();

            Assert.Equal(LandingRate.Hard, landingEvent.LandingRate);
            Assert.Equal(-35, landingEvent.ScoreDelta);
            Assert.Equal(1.6, landingEvent.TouchdownGForce);
        }

        [Fact]
        public void FinalizeLanding_FpmWorseThanG_KeepsFpmRating()
        {
            // -600 fpm is Hard (-35), 1.22 G is Perfect (+10) — the FPM delta stays.
            var tracker = CreateTracker(-600, 1.22, out var landingEvent);

            tracker.FinalizeLanding();

            Assert.Equal(LandingRate.Hard, landingEvent.LandingRate);
            Assert.Equal(-35, landingEvent.ScoreDelta);
            Assert.Equal(1.22, landingEvent.TouchdownGForce);
        }

        [Fact]
        public void FinalizeLanding_EqualDeltas_KeepsFpmRating()
        {
            // -400 fpm is Fair (-10), 1.12 G is also Fair (-10) — ties keep the FPM rating.
            var tracker = CreateTracker(-400, 1.12, out var landingEvent);

            tracker.FinalizeLanding();

            Assert.Equal(LandingRate.Fair, landingEvent.LandingRate);
            Assert.Equal(-10, landingEvent.ScoreDelta);
        }

        [Fact]
        public void FinalizeLanding_TooSoftG_PenalizesSoftLanding()
        {
            // -80 fpm is Soft (0), 1.05 G is rated Soft (-10) — the G delta wins.
            var tracker = CreateTracker(-80, 1.05, out var landingEvent);

            tracker.FinalizeLanding();

            Assert.Equal(LandingRate.Soft, landingEvent.LandingRate);
            Assert.Equal(-10, landingEvent.ScoreDelta);
        }

        [Fact]
        public void Update_PeakGWithinWindow_IsUsedForScoring()
        {
            var tracker = CreateTracker(-200, 1.2, out var landingEvent);

            tracker.Update(new FlightData { GForce = 1.45 });
            tracker.FinalizeLanding();

            Assert.Equal(1.45, landingEvent.TouchdownGForce);
            Assert.Equal(LandingRate.Fair, landingEvent.LandingRate);
            Assert.Equal(-10, landingEvent.ScoreDelta);
        }

        [Fact]
        public void Update_AfterFinalize_IsIgnored()
        {
            var tracker = CreateTracker(-200, 1.2, out var landingEvent);
            tracker.FinalizeLanding();

            tracker.Update(new FlightData { GForce = 2.5 });

            Assert.Equal(1.2, landingEvent.TouchdownGForce);
        }

        [Fact]
        public void FinalizeLanding_SecondCall_DoesNotChangeResult()
        {
            var tracker = CreateTracker(-200, 1.6, out var landingEvent);
            tracker.FinalizeLanding();
            var rating = landingEvent.LandingRate;
            var delta = landingEvent.ScoreDelta;

            tracker.FinalizeLanding();

            Assert.Equal(rating, landingEvent.LandingRate);
            Assert.Equal(delta, landingEvent.ScoreDelta);
        }

        [Fact]
        public void RegisterBounce_HigherGWithoutWorseFpm_UpdatesPeakG()
        {
            var tracker = CreateTracker(-300, 1.1, out var landingEvent);

            // Vertical speed is not worse than the recorded one, so only the peak G updates
            // (the FlightManager context is unused on this path).
            tracker.RegisterBounce(new FlightData { VerticalSpeed = -100, GForce = 1.7 }, null);
            tracker.FinalizeLanding();

            Assert.Equal(1.7, landingEvent.TouchdownGForce);
            Assert.Equal(LandingRate.Hard, landingEvent.LandingRate);
            Assert.Equal(-35, landingEvent.ScoreDelta);
        }
    }
}
