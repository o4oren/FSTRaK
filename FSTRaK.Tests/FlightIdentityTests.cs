using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class FlightIdentityTests
    {
        // One degree of latitude is 60 nm, so 0.5 degrees is 30 nm along a meridian.
        private const double OneNauticalMileInDegrees = 1.0 / 60.0;

        private static FlightIdentitySnapshot Snapshot(
            string title = "Airbus A320neo",
            string livery = "Lufthansa",
            double latitude = 40.0,
            double longitude = -74.0,
            bool onGround = false)
        {
            return new FlightIdentitySnapshot
            {
                Title = title,
                LiveryName = livery,
                Latitude = latitude,
                Longitude = longitude,
                OnGround = onGround
            };
        }

        [Fact]
        public void SameAircraftNearby_Resumes()
        {
            var before = Snapshot();
            var after = Snapshot(latitude: 40.0 + (5 * OneNauticalMileInDegrees));

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void NotInFlight_DoesNotResume()
        {
            // The user quit to the menu during the gap.
            var before = Snapshot();
            var after = Snapshot();

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: false, isMsfs2024: true));
        }

        [Fact]
        public void DifferentTitle_DoesNotResume()
        {
            var before = Snapshot(title: "Airbus A320neo");
            var after = Snapshot(title: "Cessna 172");

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void DifferentLiveryOn2024_DoesNotResume()
        {
            var before = Snapshot(livery: "Lufthansa");
            var after = Snapshot(livery: "Air France");

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void DifferentLiveryOn2020_Resumes()
        {
            // MSFS 2020 does not report a usable livery, so it must not gate the decision.
            var before = Snapshot(livery: "Lufthansa");
            var after = Snapshot(livery: "");

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: false));
        }

        [Fact]
        public void TitleComparison_IgnoresSurroundingWhitespace()
        {
            var before = Snapshot(title: "Airbus A320neo ");
            var after = Snapshot(title: " Airbus A320neo");

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void Airborne25NauticalMiles_Resumes()
        {
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: false);

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void Airborne35NauticalMiles_DoesNotResume()
        {
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (35 * OneNauticalMileInDegrees),
                onGround: false);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void OnGround25NauticalMiles_DoesNotResume()
        {
            // The ground threshold is tighter: 30 nm could span a neighbouring airport.
            var before = Snapshot(onGround: true);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void OnGround15NauticalMiles_Resumes()
        {
            var before = Snapshot(onGround: true);
            var after = Snapshot(
                latitude: 40.0 + (15 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void GroundThresholdFollowsTheReconnectedSample()
        {
            // Airborne before, on the ground after: the post-reconnect sample decides,
            // so the tighter ground threshold applies.
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void TeleportAcrossTheWorld_DoesNotResume()
        {
            var before = Snapshot(latitude: 40.0, longitude: -74.0);
            var after = Snapshot(latitude: 51.5, longitude: -0.12);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }
    }
}
