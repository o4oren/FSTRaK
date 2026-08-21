using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FSTRaK.BusinessLogic.SimBriefService.SimBriefModel
{
    /// <summary>
    /// DTOs for the SimBrief xml.fetcher.php json=1 response, limited to the subtrees FSTRaK consumes.
    /// All leaf values are strings — SimBrief emits numbers as strings and blanks as "" — parsing
    /// is done defensively in SimBriefOfpMapper.
    /// </summary>
    internal class SimBriefOfp
    {
        [JsonProperty("fetch")] public SimBriefFetch Fetch { get; set; }
        [JsonProperty("params")] public SimBriefParams Params { get; set; }
        [JsonProperty("general")] public SimBriefGeneral General { get; set; }
        [JsonProperty("origin")] public SimBriefAirport Origin { get; set; }
        [JsonProperty("destination")] public SimBriefAirport Destination { get; set; }
        // Object for a single alternate, array when multiple are planned, empty otherwise.
        [JsonProperty("alternate")] public JToken Alternate { get; set; }
        [JsonProperty("aircraft")] public SimBriefAircraft Aircraft { get; set; }
        [JsonProperty("fuel")] public SimBriefFuel Fuel { get; set; }
        [JsonProperty("times")] public SimBriefTimes Times { get; set; }
        [JsonProperty("weights")] public SimBriefWeights Weights { get; set; }
        [JsonProperty("navlog")] public SimBriefNavlog Navlog { get; set; }
    }

    internal class SimBriefFetch
    {
        [JsonProperty("status")] public string Status { get; set; }
    }

    internal class SimBriefParams
    {
        [JsonProperty("units")] public string Units { get; set; }
    }

    internal class SimBriefGeneral
    {
        [JsonProperty("icao_airline")] public string IcaoAirline { get; set; }
        [JsonProperty("flight_number")] public string FlightNumber { get; set; }
        [JsonProperty("initial_altitude")] public string InitialAltitude { get; set; }
        [JsonProperty("route")] public string Route { get; set; }
        [JsonProperty("route_distance")] public string RouteDistance { get; set; }
    }

    internal class SimBriefAirport
    {
        [JsonProperty("icao_code")] public string IcaoCode { get; set; }
        [JsonProperty("pos_lat")] public string PosLat { get; set; }
        [JsonProperty("pos_long")] public string PosLong { get; set; }
        [JsonProperty("elevation")] public string Elevation { get; set; }
    }

    internal class SimBriefAircraft
    {
        [JsonProperty("icaocode")] public string IcaoCode { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("reg")] public string Reg { get; set; }
    }

    internal class SimBriefFuel
    {
        [JsonProperty("taxi")] public string Taxi { get; set; }
        [JsonProperty("enroute_burn")] public string EnrouteBurn { get; set; }
        [JsonProperty("contingency")] public string Contingency { get; set; }
        [JsonProperty("alternate_burn")] public string AlternateBurn { get; set; }
        [JsonProperty("reserve")] public string Reserve { get; set; }
        [JsonProperty("extra")] public string Extra { get; set; }
        [JsonProperty("plan_ramp")] public string PlanRamp { get; set; }
        [JsonProperty("plan_takeoff")] public string PlanTakeoff { get; set; }
        [JsonProperty("plan_landing")] public string PlanLanding { get; set; }
    }

    internal class SimBriefTimes
    {
        [JsonProperty("est_time_enroute")] public string EstTimeEnroute { get; set; }
        [JsonProperty("est_block")] public string EstBlock { get; set; }
        [JsonProperty("sched_out")] public string SchedOut { get; set; }
        [JsonProperty("sched_in")] public string SchedIn { get; set; }
    }

    internal class SimBriefWeights
    {
        [JsonProperty("pax_count_actual")] public string PaxCountActual { get; set; }
        [JsonProperty("pax_count")] public string PaxCount { get; set; }
        [JsonProperty("bag_count")] public string BagCount { get; set; }
        [JsonProperty("cargo")] public string Cargo { get; set; }
        [JsonProperty("payload")] public string Payload { get; set; }
        [JsonProperty("est_zfw")] public string EstZfw { get; set; }
        [JsonProperty("est_tow")] public string EstTow { get; set; }
        [JsonProperty("est_ldw")] public string EstLdw { get; set; }
    }

    internal class SimBriefNavlog
    {
        [JsonProperty("fix")] public List<SimBriefFix> Fixes { get; set; }
    }

    internal class SimBriefFix
    {
        [JsonProperty("ident")] public string Ident { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("via_airway")] public string ViaAirway { get; set; }
        [JsonProperty("stage")] public string Stage { get; set; }
        [JsonProperty("is_sid_star")] public string IsSidStar { get; set; }
        [JsonProperty("pos_lat")] public string PosLat { get; set; }
        [JsonProperty("pos_long")] public string PosLong { get; set; }
        [JsonProperty("altitude_feet")] public string AltitudeFeet { get; set; }
        [JsonProperty("ind_airspeed")] public string IndAirspeed { get; set; }
        [JsonProperty("fuel_plan_onboard")] public string FuelPlanOnboard { get; set; }
        [JsonProperty("time_total")] public string TimeTotal { get; set; }
        [JsonProperty("distance")] public string Distance { get; set; }
    }
}
