using System;
using System.IO;
using System.Linq;
using FSTRaK.BusinessLogic.SimBriefService;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimBriefOfpMapperTests
    {
        private static string LoadFixture() =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "simbrief_ofp_sample.json"));

        private static FlightPlan MapFixture() => SimBriefOfpMapper.Map(SimBriefOfpMapper.Parse(LoadFixture()));

        // ── BuildFetchUrl ────────────────────────────────────────────────────

        [Fact]
        public void BuildFetchUrl_AllDigits_UsesUserId()
        {
            Assert.Equal("https://www.simbrief.com/api/xml.fetcher.php?userid=379894&json=1",
                SimBriefOfpMapper.BuildFetchUrl("379894"));
        }

        [Fact]
        public void BuildFetchUrl_NonNumeric_UsesUsername()
        {
            Assert.Equal("https://www.simbrief.com/api/xml.fetcher.php?username=oren&json=1",
                SimBriefOfpMapper.BuildFetchUrl(" oren "));
        }

        // ── Parse ────────────────────────────────────────────────────────────

        [Fact]
        public void Parse_SuccessOfp_ReturnsOfp()
        {
            var ofp = SimBriefOfpMapper.Parse(LoadFixture());
            Assert.NotNull(ofp);
            Assert.Equal("EGLL", ofp.Origin.IcaoCode);
        }

        [Fact]
        public void Parse_ErrorStatus_ReturnsNull()
        {
            const string errorJson =
                "{\"fetch\":{\"userid\":\"999999\",\"static_id\":\"\",\"status\":\"Error: No flight plan on file for the specified user\",\"time\":\"0.0002\"}}";
            Assert.Null(SimBriefOfpMapper.Parse(errorJson));
        }

        // ── Map: header fields ───────────────────────────────────────────────

        [Fact]
        public void Map_Fixture_MapsIdentityAirportsAndRoute()
        {
            var plan = MapFixture();
            Assert.Equal("BAW", plan.AirlineIcao);
            Assert.Equal("0414", plan.FlightNumber);
            Assert.Equal("BAW0414", plan.ComposedFlightNumber);
            Assert.Equal("A320", plan.AircraftType);
            Assert.Equal("A320-200", plan.AircraftName);
            Assert.Equal("G-EUUA", plan.AircraftReg);
            Assert.Equal("EGLL", plan.DepartureAirport);
            Assert.Equal("ELLX", plan.ArrivalAirport);
            Assert.Equal("EDFH", plan.AlternateAirports);
            Assert.Equal("DCT DET L6 DVR UL9 KONAN UL607 REMBA DCT", plan.Route);
            Assert.Equal(23000, plan.CruiseAltitude);
            Assert.Equal(302, plan.RouteDistanceNm);
        }

        [Fact]
        public void Map_Fixture_ConvertsKgsToLbs()
        {
            var plan = MapFixture();
            Assert.Equal(150 / Consts.LbsToKgs, plan.TaxiFuel);
            Assert.Equal(2370 / Consts.LbsToKgs, plan.EnrouteBurn);
            Assert.Equal(775 / Consts.LbsToKgs, plan.ContingencyFuel);
            Assert.Equal(1009 / Consts.LbsToKgs, plan.AlternateFuel);
            Assert.Equal(1112 / Consts.LbsToKgs, plan.ReserveFuel);
            Assert.Equal(0 / Consts.LbsToKgs, plan.ExtraFuel);
            Assert.Equal(5416 / Consts.LbsToKgs, plan.PlanRampFuel);
            Assert.Equal(5266 / Consts.LbsToKgs, plan.PlanTakeoffFuel);
            Assert.Equal(2896 / Consts.LbsToKgs, plan.PlanLandingFuel);
            Assert.Equal(3842 / Consts.LbsToKgs, plan.CargoLbs);
            Assert.Equal(15749 / Consts.LbsToKgs, plan.PayloadLbs);
            Assert.Equal(59778 / Consts.LbsToKgs, plan.EstZfw);
            Assert.Equal(65044 / Consts.LbsToKgs, plan.EstTow);
            Assert.Equal(62674 / Consts.LbsToKgs, plan.EstLdw);
        }

        [Fact]
        public void Map_LbsUnits_PassesWeightsThroughUnconverted()
        {
            var json = LoadFixture().Replace("\"units\": \"kgs\"", "\"units\": \"lbs\"");
            var plan = SimBriefOfpMapper.Map(SimBriefOfpMapper.Parse(json));
            Assert.Equal(150, plan.TaxiFuel);
            Assert.Equal(5416, plan.PlanRampFuel);
            Assert.Equal(15749, plan.PayloadLbs);
        }

        [Fact]
        public void Map_Fixture_MapsTimesAndCounts()
        {
            var plan = MapFixture();
            Assert.Equal(3121, plan.EstTimeEnrouteSec);
            Assert.Equal(4201, plan.EstBlockSec);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1786796400).UtcDateTime, plan.ScheduledOut);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1786801200).UtcDateTime, plan.ScheduledIn);
            Assert.Equal(150, plan.PaxCount);
            Assert.Equal(150, plan.BagCount);
        }

        // ── Map: navlog points ───────────────────────────────────────────────

        [Fact]
        public void Map_Fixture_PrependsOriginAndMapsAllFixes()
        {
            var plan = MapFixture();
            var points = plan.Points.OrderBy(p => p.Sequence).ToList();

            // 10 navlog fixes + prepended departure airport
            Assert.Equal(11, points.Count);

            Assert.Equal(0, points[0].Sequence);
            Assert.Equal("EGLL", points[0].Ident);
            Assert.Equal("apt", points[0].Type);

            var det = points[1];
            Assert.Equal("DET", det.Ident);
            Assert.Equal("DETLING", det.Name);
            Assert.Equal("vor", det.Type);
            Assert.Equal("CLB", det.Stage);
            Assert.False(det.IsSidStar);
            Assert.Equal(51.304003, det.Latitude, 6);
            Assert.Equal(0.597275, det.Longitude, 6);
            Assert.Equal(18100, det.AltitudeFt);
            Assert.Equal(300, det.IndicatedAirspeed);
            Assert.Equal(4444 / Consts.LbsToKgs, det.FuelOnboardLbs);
            Assert.Equal(488, det.TimeTotalSec);
            Assert.Equal(41, det.DistanceNm);

            var last = points[points.Count - 1];
            Assert.Equal("ELLX", last.Ident);
            Assert.Equal("apt", last.Type);
            Assert.Equal(3121, last.TimeTotalSec);
            Assert.Equal(2896 / Consts.LbsToKgs, last.FuelOnboardLbs);
        }

        [Fact]
        public void Map_MissingNavlog_ReturnsNull()
        {
            const string minimalJson =
                "{\"fetch\":{\"status\":\"Success\"},\"origin\":{\"icao_code\":\"EGLL\"},\"destination\":{\"icao_code\":\"ELLX\"}}";
            var ofp = SimBriefOfpMapper.Parse(minimalJson);
            Assert.NotNull(ofp);
            Assert.Null(SimBriefOfpMapper.Map(ofp));
        }

        // ── Alternate shapes ─────────────────────────────────────────────────

        [Fact]
        public void Map_AlternateAsArray_JoinsAllIcaos()
        {
            var json = LoadFixture().Replace(
                "\"alternate\":", "\"alternate_unused\":");
            // Inject a list-shaped alternate alongside the renamed original
            json = json.Substring(0, json.Length - 1) +
                   ",\"alternate\":[{\"icao_code\":\"EDFH\"},{\"icao_code\":\"EDDF\"}]}";
            var plan = SimBriefOfpMapper.Map(SimBriefOfpMapper.Parse(json));
            Assert.Equal("EDFH,EDDF", plan.AlternateAirports);
            Assert.Equal(new[] { "EDFH", "EDDF" }, plan.AlternateList);
        }

        // ── Match predicates ─────────────────────────────────────────────────

        [Fact]
        public void MatchesDeparture_ExactAndCaseInsensitive()
        {
            var plan = new FlightPlan { DepartureAirport = "EGLL" };
            Assert.True(SimBriefOfpMapper.MatchesDeparture(plan, "EGLL"));
            Assert.True(SimBriefOfpMapper.MatchesDeparture(plan, "egll"));
            Assert.False(SimBriefOfpMapper.MatchesDeparture(plan, "EGKK"));
            Assert.False(SimBriefOfpMapper.MatchesDeparture(plan, null));
            Assert.False(SimBriefOfpMapper.MatchesDeparture(null, "EGLL"));
        }

        [Fact]
        public void ShouldSavePlan_ArrivalOrAlternate()
        {
            var plan = new FlightPlan { ArrivalAirport = "ELLX", AlternateAirports = "EDFH,EDDF" };
            Assert.True(SimBriefOfpMapper.ShouldSavePlan(plan, "ELLX"));   // planned arrival
            Assert.True(SimBriefOfpMapper.ShouldSavePlan(plan, "EDFH"));   // first alternate
            Assert.True(SimBriefOfpMapper.ShouldSavePlan(plan, "eddf"));   // second alternate, case-insensitive
            Assert.False(SimBriefOfpMapper.ShouldSavePlan(plan, "EDDM"));  // landed elsewhere
            Assert.False(SimBriefOfpMapper.ShouldSavePlan(plan, null));
            Assert.False(SimBriefOfpMapper.ShouldSavePlan(null, "ELLX"));
        }
    }
}
