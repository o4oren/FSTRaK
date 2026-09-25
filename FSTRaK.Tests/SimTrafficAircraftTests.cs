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
                AtcType = "BOEING",
                AtcModel = "B738",
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

        private static SimTrafficAircraft BuildWithModel(string atcModel)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST",
                AtcType = "TESTMAKER",
                AtcModel = atcModel,
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

        private static SimTrafficAircraft BuildWithCategoryAndModel(string category, string atcModel)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST",
                AtcType = "TESTMAKER",
                AtcModel = atcModel,
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
                AtcType = "ZZZZMAKER",
                AtcModel = "ZZZZ",
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
        public void Manufacturer_ComesFromAtcType_NotFromTheModel()
        {
            // ATC Type is the manufacturer despite its name; ATC Model carries the code.
            var aircraft = Build("EIDYH", "RYR123", "Boeing 737-800");

            Assert.Equal("BOEING", aircraft.Manufacturer);
            Assert.Equal("B738", aircraft.AircraftType);
        }

        [Fact]
        public void LocalisationKeys_AreUnwrappedIntoReadableLabels()
        {
            // What the simulator actually hands back for AI objects.
            var data = new SimTrafficData
            {
                Title = "TT:ATCCOM.AC_MODEL_B738.0.text",
                AtcId = "EIDYH",
                AtcType = "ATCCOM.ATC_NAME BOEING.0.TEXT",
                AtcModel = "TT:ATCCOM.AC_MODEL_B738.0.text",
                Airline = "ATCCOM_ATC_NAME_RYANAIR",
                FlightNumber = "RYR123",
                Category = "Airplane",
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 10000,
                TrueHeading = 90,
                GroundVelocity = 300,
                SimOnGround = 0
            };

            var aircraft = new SimTrafficAircraft(new SimTrafficEntry(42, data));

            Assert.Equal("B738", aircraft.AircraftType);
            Assert.Equal("BOEING", aircraft.Manufacturer);
            Assert.Equal("RYANAIR", aircraft.Airline);
        }

        [Fact]
        public void LocalisationKeyInTheModel_StillResolvesTheIcon()
        {
            // The unwrapped code is what AircraftResolver matches on, so a wrapped key must
            // not push an otherwise-known type into the catch-all.
            var aircraft = BuildWithModel("TT:ATCCOM.AC_MODEL_A320.0.text");

            Assert.Equal("A320", aircraft.IconResource);
        }

        [Fact]
        public void IconResource_IsResolvedFromTheAtcModel()
        {
            // A320 has its own icon, and the resolver's catch-all is B737 - so a wrong
            // argument or the wrong overload would surface here as the fallback instead.
            var aircraft = BuildWithModel("A320");

            Assert.Equal("A320", aircraft.IconResource);
        }

        [Fact]
        public void IconResource_AndScaleFactor_ComeFromTheSameLookup()
        {
            // A helicopter is the one case with a distinctive scale (0.6), which catches a
            // swapped or dropped half of the resolver's (icon, scale) tuple.
            var helicopter = BuildWithModel("H135");

            Assert.Equal("Helicopter", helicopter.IconResource);
            Assert.Equal(0.6, helicopter.ScaleFactor);
        }

        [Fact]
        public void IconResource_UsesCategoryWhenTheModelIsUnrecognised()
        {
            // Proves the model actually forwards Category (and not just AircraftType) to the
            // resolver - an unrecognised ATC type would otherwise fall back to the B737 icon.
            var aircraft = BuildWithCategoryAndModel("Helicopter", "ZZZZ");

            Assert.Equal("Helicopter", aircraft.IconResource);
        }

        [Fact]
        public void IconResource_FallsBackToEngineConfiguration_WhenModelAndCategoryAreUnrecognised()
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
