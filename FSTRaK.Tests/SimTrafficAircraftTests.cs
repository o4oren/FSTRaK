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

        private static SimTrafficAircraft BuildWithType(string atcType)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST",
                AtcType = atcType,
                Airline = "TestAirline",
                FlightNumber = "TST001",
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

        private static SimTrafficAircraft BuildWithCategoryAndType(string category, string atcType)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST",
                AtcType = atcType,
                Airline = "TestAirline",
                FlightNumber = "TST001",
                Category = category,
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 12345.6,
                TrueHeading = 271.4,
                GroundVelocity = 289.7,
                SimOnGround = 0
            };

            return new SimTrafficAircraft(new SimTrafficEntry(42, data));
        }

        private static SimTrafficAircraft BuildWithEngines(int numberOfEngines, EngineType engineType)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST",
                AtcType = "ZZZZ",
                Airline = "TestAirline",
                FlightNumber = "TST001",
                Category = "Airplane",
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 12345.6,
                TrueHeading = 271.4,
                GroundVelocity = 289.7,
                SimOnGround = 0,
                NumberOfEngines = numberOfEngines,
                EngineType = engineType
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
            // A320 has its own icon, and the resolver's catch-all is B737 - so a wrong
            // argument or the wrong overload would surface here as the fallback instead.
            var aircraft = BuildWithType("A320");

            Assert.Equal("A320", aircraft.IconResource);
        }

        [Fact]
        public void IconResource_AndScaleFactor_ComeFromTheSameLookup()
        {
            // A helicopter is the one case with a distinctive scale (0.6), which catches a
            // swapped or dropped half of the resolver's (icon, scale) tuple.
            var helicopter = BuildWithType("H135");

            Assert.Equal("Helicopter", helicopter.IconResource);
            Assert.Equal(0.6, helicopter.ScaleFactor);
        }

        [Fact]
        public void IconResource_UsesCategoryWhenTheTypeIsUnrecognised()
        {
            // Proves the model actually forwards Category (and not just AircraftType) to the
            // resolver - an unrecognised ATC type would otherwise fall back to the B737 icon.
            var aircraft = BuildWithCategoryAndType("Helicopter", "ZZZZ");

            Assert.Equal("Helicopter", aircraft.IconResource);
        }

        [Fact]
        public void IconResource_FallsBackToEngineConfiguration_WhenTypeAndCategoryAreUnrecognised()
        {
            // "ZZZZ" matches none of the resolver's candidate lists and Category is
            // "Airplane" (not "Helicopter"), so this can only resolve via the engine
            // fallback - proving NumberOfEngines and EngineType are actually forwarded
            // from SimTrafficData rather than passed as literals or left at their zero
            // defaults. If the call site dropped these arguments, this would silently
            // resolve to the "B737" catch-all instead.
            var aircraft = BuildWithEngines(4, EngineType.Jet);

            Assert.Equal("A340", aircraft.IconResource);
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
