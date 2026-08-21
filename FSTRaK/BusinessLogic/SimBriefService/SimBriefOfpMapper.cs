using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FSTRaK.BusinessLogic.SimBriefService.SimBriefModel;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FSTRaK.BusinessLogic.SimBriefService
{
    /// <summary>
    /// Pure parsing/mapping/matching logic for SimBrief OFPs. No HTTP, no singletons, no WPF —
    /// this is the unit-tested surface; SimBriefService orchestrates around it.
    /// </summary>
    internal static class SimBriefOfpMapper
    {
        public static string BuildFetchUrl(string user)
        {
            var trimmed = user?.Trim() ?? string.Empty;
            var param = trimmed.Length > 0 && trimmed.All(char.IsDigit) ? "userid" : "username";
            return $"https://www.simbrief.com/api/xml.fetcher.php?{param}={Uri.EscapeDataString(trimmed)}&json=1";
        }

        public static SimBriefOfp Parse(string json)
        {
            var ofp = JsonConvert.DeserializeObject<SimBriefOfp>(json);
            return ofp?.Fetch?.Status == "Success" ? ofp : null;
        }

        public static FlightPlan Map(SimBriefOfp ofp)
        {
            if (string.IsNullOrEmpty(ofp?.Origin?.IcaoCode)
                || string.IsNullOrEmpty(ofp.Destination?.IcaoCode)
                || ofp.Navlog?.Fixes == null || ofp.Navlog.Fixes.Count == 0)
                return null;

            var isKgs = string.Equals(ofp.Params?.Units, "kgs", StringComparison.OrdinalIgnoreCase);

            var plan = new FlightPlan
            {
                AirlineIcao = ofp.General?.IcaoAirline,
                FlightNumber = ofp.General?.FlightNumber,
                AircraftType = ofp.Aircraft?.IcaoCode,
                AircraftName = ofp.Aircraft?.Name,
                AircraftReg = ofp.Aircraft?.Reg,
                DepartureAirport = ofp.Origin.IcaoCode,
                ArrivalAirport = ofp.Destination.IcaoCode,
                AlternateAirports = GetAlternateIcaos(ofp.Alternate),
                Route = ofp.General?.Route,
                CruiseAltitude = ToInt(ofp.General?.InitialAltitude),
                RouteDistanceNm = ToDouble(ofp.General?.RouteDistance),
                TaxiFuel = ToLbs(ofp.Fuel?.Taxi, isKgs),
                EnrouteBurn = ToLbs(ofp.Fuel?.EnrouteBurn, isKgs),
                ContingencyFuel = ToLbs(ofp.Fuel?.Contingency, isKgs),
                AlternateFuel = ToLbs(ofp.Fuel?.AlternateBurn, isKgs),
                ReserveFuel = ToLbs(ofp.Fuel?.Reserve, isKgs),
                ExtraFuel = ToLbs(ofp.Fuel?.Extra, isKgs),
                PlanRampFuel = ToLbs(ofp.Fuel?.PlanRamp, isKgs),
                PlanTakeoffFuel = ToLbs(ofp.Fuel?.PlanTakeoff, isKgs),
                PlanLandingFuel = ToLbs(ofp.Fuel?.PlanLanding, isKgs),
                ScheduledOut = ToUtcDateTime(ofp.Times?.SchedOut),
                ScheduledIn = ToUtcDateTime(ofp.Times?.SchedIn),
                EstTimeEnrouteSec = ToInt(ofp.Times?.EstTimeEnroute),
                EstBlockSec = ToInt(ofp.Times?.EstBlock),
                PaxCount = ToInt(ofp.Weights?.PaxCountActual) ?? ToInt(ofp.Weights?.PaxCount),
                BagCount = ToInt(ofp.Weights?.BagCount),
                CargoLbs = ToLbs(ofp.Weights?.Cargo, isKgs),
                PayloadLbs = ToLbs(ofp.Weights?.Payload, isKgs),
                EstZfw = ToLbs(ofp.Weights?.EstZfw, isKgs),
                EstTow = ToLbs(ofp.Weights?.EstTow, isKgs),
                EstLdw = ToLbs(ofp.Weights?.EstLdw, isKgs)
            };

            // The navlog starts at the first enroute fix; prepend the departure airport so the
            // drawn route starts at the field. The destination arrives as the last navlog fix (type "apt").
            var sequence = 0;
            var originLat = ToDouble(ofp.Origin.PosLat);
            var originLon = ToDouble(ofp.Origin.PosLong);
            if (originLat != null && originLon != null)
            {
                plan.Points.Add(new FlightPlanPoint
                {
                    Sequence = sequence++,
                    Ident = ofp.Origin.IcaoCode,
                    Name = ofp.Origin.IcaoCode,
                    Type = "apt",
                    Latitude = originLat.Value,
                    Longitude = originLon.Value,
                    AltitudeFt = ToInt(ofp.Origin.Elevation)
                });
            }

            foreach (var fix in ofp.Navlog.Fixes)
            {
                var lat = ToDouble(fix.PosLat);
                var lon = ToDouble(fix.PosLong);
                if (lat == null || lon == null)
                    continue;
                plan.Points.Add(new FlightPlanPoint
                {
                    Sequence = sequence++,
                    Ident = fix.Ident,
                    Name = fix.Name,
                    Type = fix.Type,
                    ViaAirway = fix.ViaAirway,
                    Stage = fix.Stage,
                    IsSidStar = fix.IsSidStar == "1",
                    Latitude = lat.Value,
                    Longitude = lon.Value,
                    AltitudeFt = ToInt(fix.AltitudeFeet),
                    IndicatedAirspeed = ToInt(fix.IndAirspeed),
                    FuelOnboardLbs = ToLbs(fix.FuelPlanOnboard, isKgs),
                    TimeTotalSec = ToInt(fix.TimeTotal),
                    DistanceNm = ToDouble(fix.Distance)
                });
            }

            return plan;
        }

        public static bool MatchesDeparture(FlightPlan plan, string departureIcao) =>
            plan != null && !string.IsNullOrEmpty(departureIcao)
            && string.Equals(plan.DepartureAirport, departureIcao, StringComparison.OrdinalIgnoreCase);

        public static bool ShouldSavePlan(FlightPlan plan, string arrivalIcao)
        {
            if (plan == null || string.IsNullOrEmpty(arrivalIcao))
                return false;
            if (string.Equals(plan.ArrivalAirport, arrivalIcao, StringComparison.OrdinalIgnoreCase))
                return true;
            return plan.AlternateList.Any(a => string.Equals(a, arrivalIcao, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetAlternateIcaos(JToken alternate)
        {
            var icaos = new List<string>();
            switch (alternate?.Type)
            {
                case JTokenType.Object:
                    AddIcao(icaos, (JObject)alternate);
                    break;
                case JTokenType.Array:
                    foreach (var alt in alternate.Children<JObject>())
                        AddIcao(icaos, alt);
                    break;
            }
            return icaos.Count > 0 ? string.Join(",", icaos) : null;
        }

        private static void AddIcao(List<string> icaos, JObject alternate)
        {
            var icao = alternate.Value<string>("icao_code");
            if (!string.IsNullOrEmpty(icao))
                icaos.Add(icao);
        }

        private static double? ToDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null;

        private static int? ToInt(string s) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;

        private static double? ToLbs(string s, bool isKgs)
        {
            var value = ToDouble(s);
            if (value == null)
                return null;
            return isKgs ? value / Consts.LbsToKgs : value;
        }

        private static DateTime? ToUtcDateTime(string epochSeconds)
        {
            if (!long.TryParse(epochSeconds, out var epoch))
                return null;
            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }
    }
}
