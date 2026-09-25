using FSTRaK.DataTypes;
using FSTRaK.Utils;
using Xunit;

namespace FSTRaK.Tests
{
    public class AircraftResolverTests
    {
        [Fact]
        public void Category_Helicopter_WinsOverAnUnknownType()
        {
            var (icon, scale) = AircraftResolver.GetAircraftIcon("Helicopter", "ZZZZ", 1, EngineType.HeloTurbine);

            Assert.Equal("Helicopter", icon);
            Assert.Equal(0.6, scale);
        }

        [Fact]
        public void Category_IsMatchedCaseInsensitivelyAndTrimmed()
        {
            var (icon, _) = AircraftResolver.GetAircraftIcon("  HELICOPTER ", "ZZZZ", 1, EngineType.HeloTurbine);

            Assert.Equal("Helicopter", icon);
        }

        [Fact]
        public void KnownType_WinsOverTheEngineHeuristics()
        {
            // A320 is in the type table; the single-piston heuristic must not override it.
            var (icon, _) = AircraftResolver.GetAircraftIcon("Airplane", "A320", 1, EngineType.Piston);

            Assert.Equal("A320", icon);
        }

        [Fact]
        public void UnknownType_SinglePiston_ResolvesToALightAircraft()
        {
            var (icon, scale) = AircraftResolver.GetAircraftIcon("Airplane", "ZZZZ", 1, EngineType.Piston);

            Assert.Equal("C172", icon);
            Assert.Equal(0.6, scale);
        }

        [Fact]
        public void UnknownType_TwinPiston_ResolvesToATwin()
        {
            var (icon, _) = AircraftResolver.GetAircraftIcon("Airplane", "ZZZZ", 2, EngineType.Piston);

            Assert.Equal("B200", icon);
        }

        [Fact]
        public void UnknownType_FourJets_ResolvesToAQuad()
        {
            var (icon, scale) = AircraftResolver.GetAircraftIcon("Airplane", "ZZZZ", 4, EngineType.Jet);

            Assert.Equal("A340", icon);
            Assert.Equal(0.9, scale);
        }

        [Fact]
        public void UnknownType_NoMatchingEngineConfiguration_FallsBackToAnAirliner()
        {
            var (icon, _) = AircraftResolver.GetAircraftIcon("Airplane", "ZZZZ", 2, EngineType.Jet);

            Assert.Equal("B737", icon);
        }

        [Fact]
        public void NullCategoryAndType_DoNotThrow()
        {
            var (icon, _) = AircraftResolver.GetAircraftIcon(null, null, 0, EngineType.None);

            Assert.Equal("B737", icon);
        }
    }
}
