using FSTRaK.Utils;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimVarTextTests
    {
        [Fact]
        public void Humanize_ModelKeyWithTtPrefix_YieldsTheIcaoCode()
        {
            Assert.Equal("B738", SimVarText.Humanize("TT:ATCCOM.AC_MODEL_B738.0.text"));
        }

        [Fact]
        public void Humanize_ModelKeyWithASpaceSeparator_YieldsTheIcaoCode()
        {
            Assert.Equal("B738", SimVarText.Humanize("TT:ATCCOM.AC_MODEL B738.0.text"));
        }

        [Fact]
        public void Humanize_AirlineKeyInUpperCase_YieldsTheName()
        {
            Assert.Equal("AIRBUS", SimVarText.Humanize("ATCCOM.ATC_NAME AIRBUS.0.TEXT"));
        }

        [Fact]
        public void Humanize_KeyWithNoTrailingMarker_StillYieldsTheName()
        {
            // Observed in the wild, possibly truncated. The parser must not depend on the
            // ".0.text" tail being present.
            Assert.Equal("BEECHCRAFT", SimVarText.Humanize("ATCCOM_AC_MODEL_BEECHCRAFT"));
        }

        [Fact]
        public void Humanize_MultiWordName_KeepsEveryWord()
        {
            Assert.Equal("AIR FRANCE", SimVarText.Humanize("TT:ATCCOM.ATC_NAME AIR FRANCE.0.text"));
        }

        [Fact]
        public void Humanize_RealName_IsLeftAlone()
        {
            // No simulator markers, so nothing is taken apart - underscores and dots in a
            // genuine name must survive.
            Assert.Equal("Boeing 737-800", SimVarText.Humanize("Boeing 737-800"));
            Assert.Equal("A320", SimVarText.Humanize("A320"));
        }

        [Fact]
        public void Humanize_KeyThatReducesToNothing_ReturnsTheOriginal()
        {
            // Every token is scaffolding, so there is no name to show. An ugly label beats
            // a blank one.
            Assert.Equal("ATCCOM.AC_MODEL.0.text", SimVarText.Humanize("ATCCOM.AC_MODEL.0.text"));
        }

        [Fact]
        public void Humanize_NullOrEmpty_IsReturnedUnchanged()
        {
            Assert.Null(SimVarText.Humanize(null));
            Assert.Equal("", SimVarText.Humanize(""));
        }

        [Fact]
        public void Humanize_SurroundingWhitespace_IsTrimmed()
        {
            Assert.Equal("B738", SimVarText.Humanize("  TT:ATCCOM.AC_MODEL_B738.0.text  "));
        }
    }
}
