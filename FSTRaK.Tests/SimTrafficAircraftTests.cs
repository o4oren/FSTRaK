using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using FSTRaK.ViewModels;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimTrafficAircraftTests
    {
        private static SimTrafficAircraft Build(string atcId, string flightNumber, string title)
        {
            var data = new SimTrafficData
            {
                Title = title,
                AtcId = atcId,
                AtcType = "B738",
                Airline = "Ryanair",
                FlightNumber = flightNumber,
                Category = "Airplane",
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 12345.6,
                TrueHeading = 271.4,
                GroundVelocity = 289.7,
                SimOnGround = 0
            };

            return new SimTrafficAircraft(new SimTrafficEntry(42, data));
        }

        [Fact]
        public void Callsign_PrefersAtcId()
        {
            Assert.Equal("EIDYH", Build("EIDYH", "RYR123", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Callsign_FallsBackToFlightNumberWhenAtcIdIsBlank()
        {
            Assert.Equal("RYR123", Build("   ", "RYR123", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Callsign_FallsBackToTitleWhenAtcIdAndFlightNumberAreBlank()
        {
            Assert.Equal("Boeing 737-800", Build("", "", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Location_UsesTheReportedCoordinates()
        {
            var aircraft = Build("EIDYH", "", "Boeing 737-800");

            Assert.Equal(51.5, aircraft.Location.Latitude);
            Assert.Equal(-0.45, aircraft.Location.Longitude);
        }

        [Fact]
        public void AltitudeAndGroundspeed_AreRoundedToWholeUnits()
        {
            var aircraft = Build("EIDYH", "", "Boeing 737-800");

            Assert.Equal(12346, aircraft.Altitude);
            Assert.Equal(290, aircraft.Groundspeed);
        }

        [Fact]
        public void IconResource_IsResolvedFromTheAtcType()
        {
            Assert.False(string.IsNullOrEmpty(Build("EIDYH", "", "Boeing 737-800").IconResource));
        }

        [Fact]
        public void TooltipText_IncludesTheCallsignAndType()
        {
            var tooltip = Build("EIDYH", "", "Boeing 737-800").TooltipText;

            Assert.Contains("EIDYH", tooltip);
            Assert.Contains("B738", tooltip);
        }
    }
}
