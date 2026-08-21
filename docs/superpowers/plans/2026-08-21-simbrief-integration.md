# SimBrief Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fetch the user's latest SimBrief OFP at flight start, overlay the planned route on the live map, and persist plan data with the flight for planned-vs-actual display in the logbook. Ships as v3.7.0.

**Architecture:** A new event-driven `SimBriefService` singleton observes `FlightManager` state transitions and fetches/matches the OFP at two checkpoints. A pure `SimBriefOfpMapper` (unit-tested) handles parsing, mapping to new EF6 entities (`FlightPlan` 1:0..1 with `Flight`, `FlightPlanPoint` children), unit normalization, and match predicates. UI binds to the service (live map) or to `Flight.FlightPlan` (logbook/details).

**Tech Stack:** .NET Framework 4.7.2, WPF/MVVM, EF6 + SQLite (automatic migrations), Newtonsoft.Json, XAML.MapControl, xUnit (FSTRaK.Tests), Serilog.

**Spec:** `docs/superpowers/specs/2026-08-21-simbrief-integration-design.md`

## Global Constraints

- **No local build or test runs.** This machine (macOS) cannot run MSBuild/vstest. Write tests first, implement, commit — the GitHub Actions `Tests` workflow builds `FSTRaK.Tests.csproj` (Release|x64) and runs xUnit on every PR. Steps marked "CI-verified" cannot be run locally; do not claim they passed.
- **Main app project file is `FSTRaK/FSTrAk.csproj`** (mixed case). Every new `.cs` file under `FSTRaK/` MUST be added as a `<Compile Include="..."/>` item or it will not build. Git also tracks the alias path `FSTRaK/FSTRaK.csproj`; it is synced ONCE in the final task (Task 11) — do not try `git add FSTRaK/FSTRaK.csproj` (it stages nothing on this case-insensitive filesystem).
- `FSTRaK.Tests` is an SDK-style project — test `.cs` files are auto-globbed, no csproj entry needed for code (only for the fixture copy item, Task 2).
- All weights/fuel stored in the DB are **lbs**. SimBrief OFPs report in the unit given by `params.units` (`"kgs"` or `"lbs"`); convert kgs→lbs by dividing by `Consts.LbsToKgs` (0.45359237).
- All SimBrief failures are silent: log and continue. Nothing SimBrief-related may ever prevent a flight from being saved.
- `InternalsVisibleTo("FSTRaK.Tests")` already exists in `FSTRaK/Properties/AssemblyInfo.cs` — internal classes are directly testable.
- Naming collision: `FSTRaK.BusinessLogic.VatsimService.VatsimModel.FlightPlan` already exists. The new entity is `FSTRaK.Models.FlightPlan`. In any file importing both namespaces (e.g. `LiveViewViewModel`), avoid bare `FlightPlan` type references (use `var`) or add `using EntityFlightPlan = FSTRaK.Models.FlightPlan;`.
- Do not push to remote; the user pushes (a corporate hook blocks pushes from this environment).
- Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## Fixture (already committed alongside this plan)

`FSTRaK.Tests/Fixtures/simbrief_ofp_sample.json` — a real OFP fetched from the user's SimBrief account 2026-08-21, trimmed to the consumed subtrees. Known values used in test expectations:

| Field | Value |
|---|---|
| `params.units` | `"kgs"` (all fixture weights are kg) |
| origin / destination / alternate | EGLL / ELLX / EDFH (alternate is a single JSON **object**) |
| `general`: icao_airline / flight_number / route / initial_altitude / route_distance | `BAW` / `0414` / `DCT DET L6 DVR UL9 KONAN UL607 REMBA DCT` / 23000 / 302 |
| `aircraft`: icaocode / name / reg | A320 / A320-200 / G-EUUA |
| `fuel` (kg): taxi / enroute_burn / contingency / alternate_burn / reserve / extra / plan_takeoff / plan_ramp / plan_landing | 150 / 2370 / 775 / 1009 / 1112 / 0 / 5266 / 5416 / 2896 |
| `times`: est_time_enroute / est_block / sched_out / sched_in | 3121 / 4201 / 1786796400 / 1786801200 (epoch s) |
| `weights` (kg): pax_count_actual / bag_count / cargo / payload / est_zfw / est_tow / est_ldw | 150 / 150 / 3842 / 15749 / 59778 / 65044 / 62674 |
| `navlog.fix` | 10 fixes: DET(vor), TOC(ltlg), DVR, KONAN, KOK, FERDI, BUPAL, REMBA, TOD(ltlg), ELLX(**apt**) |
| fix DET | via `DCT`, lat 51.304003, lon 0.597275, alt 18100, IAS 300, fuel_plan_onboard 4444, time_total 488, distance 41 |
| fix ELLX (last) | type `apt`, fuel_plan_onboard 2896, time_total 3121 |

---

### Task 1: `FlightPlan` / `FlightPlanPoint` entities and EF wiring

**Files:**
- Create: `FSTRaK/Models/Entity/FlightPlan.cs`
- Create: `FSTRaK/Models/Entity/FlightPlanPoint.cs`
- Modify: `FSTRaK/Models/Entity/Flight.cs` (add navigation + display properties)
- Modify: `FSTRaK/Models/Entity/LogbookContext.cs` (DbSets + fluent mapping)
- Modify: `FSTRaK/FSTrAk.csproj` (register the two new files)

**Interfaces:**
- Consumes: existing `Flight`, `Aircraft`, `LogbookContext`, `UnitsUtil.GetWeightString(double?)`.
- Produces: `FSTRaK.Models.FlightPlan` (public; shared-PK 1:0..1 with `Flight`; `ComposedFlightNumber`, `AlternateList` helpers), `FSTRaK.Models.FlightPlanPoint` (public; `TooltipText` helper), `Flight.FlightPlan` (virtual nav), `Flight.PlanFlightNumber`, `Flight.DisplayAirline`, `LogbookContext.FlightPlans`, `LogbookContext.FlightPlanPoints`. All later tasks depend on these exact names.

- [ ] **Step 1: Create `FSTRaK/Models/Entity/FlightPlan.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    /// <summary>
    /// A SimBrief OFP captured for a flight. Weights and fuel are stored in lbs.
    /// Shares its primary key with the owning Flight (EF6 one-to-optional-one).
    /// </summary>
    public class FlightPlan
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("ID")]
        public int Id { get; set; }

        public virtual Flight Flight { get; set; }

        public string AirlineIcao { get; set; }
        public string FlightNumber { get; set; }

        public string AircraftType { get; set; }
        public string AircraftName { get; set; }
        public string AircraftReg { get; set; }

        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        // Comma-joined ICAO idents; SimBrief can plan multiple alternates.
        public string AlternateAirports { get; set; }

        public string Route { get; set; }
        public int? CruiseAltitude { get; set; }
        public double? RouteDistanceNm { get; set; }

        public double? TaxiFuel { get; set; }
        public double? EnrouteBurn { get; set; }
        public double? ContingencyFuel { get; set; }
        public double? AlternateFuel { get; set; }
        public double? ReserveFuel { get; set; }
        public double? ExtraFuel { get; set; }
        public double? PlanRampFuel { get; set; }
        public double? PlanTakeoffFuel { get; set; }
        public double? PlanLandingFuel { get; set; }

        public DateTime? ScheduledOut { get; set; }
        public DateTime? ScheduledIn { get; set; }
        public int? EstTimeEnrouteSec { get; set; }
        public int? EstBlockSec { get; set; }

        public int? PaxCount { get; set; }
        public int? BagCount { get; set; }
        public double? CargoLbs { get; set; }
        public double? PayloadLbs { get; set; }
        public double? EstZfw { get; set; }
        public double? EstTow { get; set; }
        public double? EstLdw { get; set; }

        public virtual ICollection<FlightPlanPoint> Points { get; set; } = new List<FlightPlanPoint>();

        [NotMapped]
        public string ComposedFlightNumber =>
            string.IsNullOrWhiteSpace(AirlineIcao) && string.IsNullOrWhiteSpace(FlightNumber)
                ? null
                : $"{AirlineIcao}{FlightNumber}";

        [NotMapped]
        public string[] AlternateList =>
            string.IsNullOrEmpty(AlternateAirports) ? Array.Empty<string>() : AlternateAirports.Split(',');
    }
}
```

- [ ] **Step 2: Create `FSTRaK/Models/Entity/FlightPlanPoint.cs`**

```csharp
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FSTRaK.Utils;

namespace FSTRaK.Models
{
    /// <summary>
    /// One navlog fix of a SimBrief flight plan. Sequence 0 is the departure airport.
    /// FuelOnboardLbs is stored in lbs.
    /// </summary>
    public class FlightPlanPoint
    {
        [Column("ID")]
        public int Id { get; set; }

        public int FlightPlanId { get; set; }
        public virtual FlightPlan FlightPlan { get; set; }

        public int Sequence { get; set; }
        public string Ident { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string ViaAirway { get; set; }
        public string Stage { get; set; }
        public bool IsSidStar { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? AltitudeFt { get; set; }
        public int? IndicatedAirspeed { get; set; }
        public double? FuelOnboardLbs { get; set; }
        public int? TimeTotalSec { get; set; }
        public double? DistanceNm { get; set; }

        [NotMapped]
        public string TooltipText
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Ident);
                if (!string.IsNullOrEmpty(Name) && Name != Ident)
                    sb.Append($" ({Name})");
                if (!string.IsNullOrEmpty(ViaAirway) && ViaAirway != "DCT")
                    sb.Append($" via {ViaAirway}");
                if (AltitudeFt != null)
                    sb.Append($"\nPlanned altitude: {AltitudeFt} ft");
                if (IndicatedAirspeed != null)
                    sb.Append($"\nPlanned IAS: {IndicatedAirspeed} kts");
                if (FuelOnboardLbs != null)
                    sb.Append($"\nPlanned fuel onboard: {UnitsUtil.GetWeightString(FuelOnboardLbs)}");
                if (TimeTotalSec != null)
                    sb.Append($"\nElapsed: {TimeSpan.FromSeconds(TimeTotalSec.Value):hh\\:mm}");
                return sb.ToString();
            }
        }
    }
}
```

- [ ] **Step 3: Add navigation + display properties to `FSTRaK/Models/Entity/Flight.cs`**

After the `Comment` property (around line 151), add:

```csharp
        public virtual FlightPlan FlightPlan { get; set; }

        [NotMapped]
        public string PlanFlightNumber => FlightPlan?.ComposedFlightNumber;

        // Airline for display: the aircraft record wins; the plan's airline ICAO is the fallback.
        [NotMapped]
        public string DisplayAirline =>
            !string.IsNullOrWhiteSpace(Aircraft?.Airline) ? Aircraft.Airline : FlightPlan?.AirlineIcao;
```

- [ ] **Step 4: Register entities in `FSTRaK/Models/Entity/LogbookContext.cs`**

Add DbSets after `public DbSet<Aircraft> Aircraft { get; set; }`:

```csharp
        public DbSet<FlightPlan> FlightPlans { get; set; }
        public DbSet<FlightPlanPoint> FlightPlanPoints { get; set; }
```

Add mapping inside `OnModelCreating`, after the existing `Flight`→`FlightEvents` mapping:

```csharp
            modelBuilder.Entity<Flight>()
                .HasOptional(f => f.FlightPlan)
                .WithRequired(p => p.Flight)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<FlightPlan>()
                .HasMany(p => p.Points)
                .WithRequired(pt => pt.FlightPlan)
                .HasForeignKey(pt => pt.FlightPlanId)
                .WillCascadeOnDelete(true);
```

No manual migration is needed — EF6 automatic migrations create both tables on first run.

- [ ] **Step 5: Register the two new files in `FSTRaK/FSTrAk.csproj`**

Next to the existing `<Compile Include="Models\Entity\Airport.cs" />` entry, add:

```xml
    <Compile Include="Models\Entity\FlightPlan.cs" />
    <Compile Include="Models\Entity\FlightPlanPoint.cs" />
```

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/Models/Entity/FlightPlan.cs FSTRaK/Models/Entity/FlightPlanPoint.cs FSTRaK/Models/Entity/Flight.cs FSTRaK/Models/Entity/LogbookContext.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: FlightPlan and FlightPlanPoint entities for SimBrief integration"
```

---

### Task 2: SimBrief DTOs and test fixture wiring

**Files:**
- Create: `FSTRaK/BusinessLogic/SimBriefService/SimBriefModel/SimBriefOfp.cs`
- Modify: `FSTRaK/FSTrAk.csproj`
- Modify: `FSTRaK.Tests/FSTRaK.Tests.csproj` (fixture copy-to-output)
- Existing: `FSTRaK.Tests/Fixtures/simbrief_ofp_sample.json` (committed with this plan)

**Interfaces:**
- Consumes: Newtonsoft.Json (already referenced by the app project).
- Produces: `FSTRaK.BusinessLogic.SimBriefService.SimBriefModel.SimBriefOfp` and nested DTOs (`Fetch.Status`, `Params.Units`, `General`, `Origin`/`Destination` (`SimBriefAirport`), `Alternate` (raw `JToken`), `Aircraft`, `Fuel`, `Times`, `Weights`, `Navlog.Fixes`). All leaf values are `string` — SimBrief emits numbers as strings and empty values as `""`; parsing happens in the mapper (Task 3).

- [ ] **Step 1: Create `FSTRaK/BusinessLogic/SimBriefService/SimBriefModel/SimBriefOfp.cs`**

```csharp
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
```

- [ ] **Step 2: Register the DTO file in `FSTRaK/FSTrAk.csproj`**

Next to the `<Compile Include="BusinessLogic\VatsimService\VatsimService.cs" />` entry, add:

```xml
    <Compile Include="BusinessLogic\SimBriefService\SimBriefModel\SimBriefOfp.cs" />
```

- [ ] **Step 3: Make the fixture copy to the test output directory**

In `FSTRaK.Tests/FSTRaK.Tests.csproj`, add before `</Project>`:

```xml
  <ItemGroup>
    <None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/BusinessLogic/SimBriefService/SimBriefModel/SimBriefOfp.cs FSTRaK/FSTrAk.csproj FSTRaK.Tests/FSTRaK.Tests.csproj
git commit -m "feat: SimBrief OFP DTOs and test fixture wiring"
```

---

### Task 3: `SimBriefOfpMapper` — parse, map, match (TDD)

**Files:**
- Create: `FSTRaK.Tests/SimBriefOfpMapperTests.cs` (test first)
- Create: `FSTRaK/BusinessLogic/SimBriefService/SimBriefOfpMapper.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: Task 2 DTOs, Task 1 entities, `Consts.LbsToKgs`.
- Produces (all `internal static` on `SimBriefOfpMapper`, namespace `FSTRaK.BusinessLogic.SimBriefService`):
  - `string BuildFetchUrl(string user)` — all-digits → `userid=`, else `username=`, always `&json=1`.
  - `SimBriefOfp Parse(string json)` — null unless `fetch.status == "Success"`.
  - `FlightPlan Map(SimBriefOfp ofp)` — null when origin/destination/navlog missing; prepends the departure airport as `Sequence` 0 point (type `"apt"`); converts kgs→lbs when `params.units == "kgs"`.
  - `bool MatchesDeparture(FlightPlan plan, string departureIcao)` — case-insensitive exact ICAO.
  - `bool ShouldSavePlan(FlightPlan plan, string arrivalIcao)` — true for planned arrival or any alternate.

- [ ] **Step 1: Write the failing tests — `FSTRaK.Tests/SimBriefOfpMapperTests.cs`**

```csharp
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
```

- [ ] **Step 2: Verify the tests fail** — CI-verified only (no local build). The compile error "SimBriefOfpMapper does not exist" is the expected failure mode; proceed.

- [ ] **Step 3: Create `FSTRaK/BusinessLogic/SimBriefService/SimBriefOfpMapper.cs`**

```csharp
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
```

- [ ] **Step 4: Register in `FSTRaK/FSTrAk.csproj`** (next to the DTO entry from Task 2):

```xml
    <Compile Include="BusinessLogic\SimBriefService\SimBriefOfpMapper.cs" />
```

- [ ] **Step 5: Commit** (tests pass CI-verified on the eventual PR)

```bash
git add FSTRaK.Tests/SimBriefOfpMapperTests.cs FSTRaK/BusinessLogic/SimBriefService/SimBriefOfpMapper.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: SimBrief OFP mapper with parsing, unit normalization and match rules"
```

---

### Task 4: `SimbriefUser` setting

**Files:**
- Modify: `FSTRaK/Properties/Settings.settings`
- Modify: `FSTRaK/Properties/Settings.Designer.cs`
- Modify: `FSTRaK/App.config`
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`
- Modify: `FSTRaK/Views/SettingsView.xaml`

**Interfaces:**
- Produces: `Properties.Settings.Default.SimbriefUser` (string, default `""`). Task 5's service reads it.

- [ ] **Step 1: Add to `Settings.settings`** — after the `VatsimId` setting element, add:

```xml
    <Setting Name="SimbriefUser" Type="System.String" Scope="User">
      <Value Profile="(Default)" />
    </Setting>
```

- [ ] **Step 2: Add to `Settings.Designer.cs`** — after the `VatsimId` property (copy its exact attribute pattern):

```csharp
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string SimbriefUser {
            get {
                return ((string)(this["SimbriefUser"]));
            }
            set {
                this["SimbriefUser"] = value;
            }
        }
```

- [ ] **Step 3: Add to `App.config`** — after the `VatsimId` setting element:

```xml
      <setting name="SimbriefUser" serializeAs="String">
        <value />
      </setting>
```

- [ ] **Step 4: Add view-model property in `SettingsViewModel.cs`** — after the `VatsimId` property (~line 369):

```csharp
        private string _simbriefUser;
        public string SimbriefUser
        {
            get => _simbriefUser;
            set
            {
                _simbriefUser = value;
                Properties.Settings.Default.SimbriefUser = _simbriefUser;
                OnPropertyChanged();
            }
        }
```

And in the load section where `VatsimId = Properties.Settings.Default.VatsimId;` appears (~line 480), add:

```csharp
            SimbriefUser = Properties.Settings.Default.SimbriefUser;
```

- [ ] **Step 5: Add the textbox row in `SettingsView.xaml`** — after the VATSIM ID StackPanel (ends ~line 315), following the identical row pattern:

```xml
                    <StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
                        <Label Style="{DynamicResource FSTrAkLabel}" Width="250">SimBrief username / Pilot ID</Label>
                        <TextBox FontFamily="{DynamicResource CurrentFont}"
                                 Foreground="{DynamicResource TextColor}"
                                 Background="{DynamicResource ControlBackgroundColorBrush}"
                                 FontSize="{DynamicResource ControlFontSize}"
                                 Width="200"
                                 Text="{Binding SimbriefUser}" Cursor="Arrow" TextAlignment="Center" Padding="0 6 0 0"/>
                        <StackPanel.ToolTip>
                            Enter your SimBrief username or numeric Pilot ID to fetch your flight plan when a flight starts. Leave empty to disable SimBrief integration.
                        </StackPanel.ToolTip>
                    </StackPanel>
```

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/Properties/Settings.settings FSTRaK/Properties/Settings.Designer.cs FSTRaK/App.config FSTRaK/ViewModels/SettingsViewModel.cs FSTRaK/Views/SettingsView.xaml
git commit -m "feat: SimbriefUser setting with settings UI"
```

---

### Task 5: `SimBriefService` singleton with checkpoint lifecycle

**Files:**
- Create: `FSTRaK/BusinessLogic/SimBriefService/SimBriefService.cs`
- Modify: `FSTRaK/Views/MainWindow.xaml.cs` (initialize after FlightManager)
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: `FlightManager.Instance` (`State`, `ActiveFlight` PropertyChanged), `Flight.DepartureAirport`, `SimBriefOfpMapper`, `Properties.Settings.Default.SimbriefUser`, `BaseModel`.
- Produces: `SimBriefService.Instance` (singleton), `SimBriefService.MatchedFlightPlan` (`FSTRaK.Models.FlightPlan`, null when no relevant plan; raises `PropertyChanged` — **possibly from a non-UI thread**), `SimBriefService.Initialize()`. Tasks 6 and 7 consume `MatchedFlightPlan`.

No unit tests for this class — it is a thin event orchestrator around the tested mapper; behavior is covered by the manual checklist. The spec's "empty setting → dormant" rule is enforced here (the `string.IsNullOrEmpty(user)` guard at the top of `FetchAndMatchAsync`) and verified by manual check #1.

- [ ] **Step 1: Create `FSTRaK/BusinessLogic/SimBriefService/SimBriefService.cs`**

```csharp
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using FSTRaK.BusinessLogic.FlightManager.State;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.SimBriefService
{
    /// <summary>
    /// Event-driven SimBrief integration. Fetches the user's latest OFP at two checkpoints —
    /// flight started (departure airport resolved) and taxi out — and exposes the plan when its
    /// departure matches the detected departure airport. Checkpoint 2 is the source of truth:
    /// it always fetches and replaces a checkpoint-1 match on success. No polling, no mid-flight
    /// refresh. All failures are logged and swallowed.
    /// </summary>
    internal sealed class SimBriefService : BaseModel
    {
        private static readonly object Lock = new();
        private static SimBriefService _instance;

        public static SimBriefService Instance
        {
            get
            {
                lock (Lock)
                    return _instance ??= new SimBriefService();
            }
        }

        private SimBriefService() { }

        private FlightManager.FlightManager _flightManager;
        private Flight _subscribedFlight;
        private bool _checkpoint2Done;

        private FlightPlan _matchedFlightPlan;
        public FlightPlan MatchedFlightPlan
        {
            get => _matchedFlightPlan;
            private set
            {
                if (ReferenceEquals(_matchedFlightPlan, value)) return;
                _matchedFlightPlan = value;
                OnPropertyChanged();
            }
        }

        public void Initialize()
        {
            _flightManager = FlightManager.FlightManager.Instance;
            _flightManager.PropertyChanged += FlightManagerOnPropertyChanged;
        }

        private void FlightManagerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FlightManager.FlightManager.ActiveFlight):
                    // Raised on every FlightData tick — only rewire when the flight instance changed.
                    if (ReferenceEquals(_subscribedFlight, _flightManager.ActiveFlight)) return;
                    if (_subscribedFlight != null)
                        _subscribedFlight.PropertyChanged -= FlightOnPropertyChanged;
                    _subscribedFlight = _flightManager.ActiveFlight;
                    if (_subscribedFlight != null)
                        _subscribedFlight.PropertyChanged += FlightOnPropertyChanged;
                    break;

                case nameof(FlightManager.FlightManager.State):
                    OnStateChanged();
                    break;
            }
        }

        private void OnStateChanged()
        {
            switch (_flightManager.State)
            {
                case FlightStartedState _:
                case SimNotInFlightState _:
                    Reset();
                    break;
                case TaxiOutState _ when !_checkpoint2Done:
                    _checkpoint2Done = true;
                    _ = FetchAndMatchAsync("checkpoint 2 (taxi out)");
                    break;
            }
        }

        private void FlightOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Checkpoint 1 — the departure airport resolves asynchronously after FlightStartedState is entered.
            if (e.PropertyName != nameof(Flight.DepartureAirport)) return;
            if (_flightManager.State is not FlightStartedState) return;
            if (_checkpoint2Done || MatchedFlightPlan != null) return;
            if (string.IsNullOrEmpty(_subscribedFlight?.DepartureAirport)) return;
            _ = FetchAndMatchAsync("checkpoint 1 (flight started)");
        }

        private void Reset()
        {
            _checkpoint2Done = false;
            MatchedFlightPlan = null;
        }

        private async Task FetchAndMatchAsync(string checkpoint)
        {
            var user = Properties.Settings.Default.SimbriefUser?.Trim();
            if (string.IsNullOrEmpty(user)) return;
            var departure = _flightManager.ActiveFlight?.DepartureAirport;
            if (string.IsNullOrEmpty(departure)) return;

            try
            {
                Log.Information($"SimBrief: fetching latest OFP at {checkpoint}");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var json = await client.GetStringAsync(SimBriefOfpMapper.BuildFetchUrl(user));

                var ofp = SimBriefOfpMapper.Parse(json);
                if (ofp == null)
                {
                    Log.Information("SimBrief: no valid OFP on file");
                    return;
                }

                var plan = SimBriefOfpMapper.Map(ofp);
                if (plan == null)
                {
                    Log.Warning("SimBrief: OFP could not be mapped (missing airports or navlog)");
                    return;
                }

                if (!SimBriefOfpMapper.MatchesDeparture(plan, departure))
                {
                    Log.Information($"SimBrief: OFP departure {plan.DepartureAirport} does not match {departure} - ignoring");
                    return;
                }

                Log.Information($"SimBrief: matched plan {plan.DepartureAirport} -> {plan.ArrivalAirport} at {checkpoint}");
                MatchedFlightPlan = plan;
            }
            catch (Exception ex)
            {
                // On a checkpoint-2 failure a checkpoint-1 match is intentionally kept.
                Log.Warning(ex, "SimBrief: failed to fetch or parse OFP");
            }
        }
    }
}
```

- [ ] **Step 2: Initialize in `MainWindow.xaml.cs`** — in `OnLoad`, directly after `_flightManager.Initialize();`:

```csharp
            BusinessLogic.SimBriefService.SimBriefService.Instance.Initialize();
```

(Fully qualified to avoid a `using` that would pull the `SimBriefService` namespace into scope unnecessarily.)

- [ ] **Step 3: Register in `FSTRaK/FSTrAk.csproj`:**

```xml
    <Compile Include="BusinessLogic\SimBriefService\SimBriefService.cs" />
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/BusinessLogic/SimBriefService/SimBriefService.cs FSTRaK/Views/MainWindow.xaml.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: SimBriefService with two-checkpoint fetch-and-match lifecycle"
```

---

### Task 6: Live map overlay and toggle

**Files:**
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`
- Modify: `FSTRaK/Views/LiveView.xaml`
- Modify: `FSTRaK/Resources/Theme.xaml`, `FSTRaK/Resources/DarkTheme.xaml` (new brush)

**Interfaces:**
- Consumes: `SimBriefService.Instance.MatchedFlightPlan` + its `PropertyChanged` (may fire on a background thread — marshal via Dispatcher), `FlightPlanPoint.TooltipText`.
- Produces (on `LiveViewViewModel`): `IsFlightPlanAvailable` (bool), `IsShowFlightPlan` (bool, two-way), `IsShowFlightPlanOverlay` (bool, AND of the two), `PlannedRouteLocations` (`ObservableCollection<Location>`), `PlannedWaypoints` (`ObservableCollection<PlannedWaypoint>` with `Location`/`Ident`/`Tooltip`).

- [ ] **Step 1: Add the brush to both theme dictionaries**

In `FSTRaK/Resources/Theme.xaml` next to `FlightPathColorBrush` (~line 102) and in `FSTRaK/Resources/DarkTheme.xaml` (~line 109), add:

```xml
    <SolidColorBrush x:Key="PlannedPathColorBrush" Color="#7B68EE"/>
```

- [ ] **Step 2: Add state and collections to `LiveViewViewModel.cs`**

Add the service field next to the other service fields (~line 26):

```csharp
        private readonly BusinessLogic.SimBriefService.SimBriefService _simBriefService =
            BusinessLogic.SimBriefService.SimBriefService.Instance;
```

Add near the other collections (~line 411):

```csharp
        public ObservableCollection<Location> PlannedRouteLocations { get; } = new();
        public ObservableCollection<PlannedWaypoint> PlannedWaypoints { get; } = new();

        public bool IsFlightPlanAvailable => _simBriefService.MatchedFlightPlan != null;

        private bool _isShowFlightPlan;
        public bool IsShowFlightPlan
        {
            get => _isShowFlightPlan;
            set
            {
                if (_isShowFlightPlan == value) return;
                _isShowFlightPlan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShowFlightPlanOverlay));
            }
        }

        public bool IsShowFlightPlanOverlay => IsFlightPlanAvailable && IsShowFlightPlan;

        internal class PlannedWaypoint
        {
            public Location Location { get; set; }
            public string Ident { get; set; }
            public string Tooltip { get; set; }
        }
```

- [ ] **Step 3: Subscribe and rebuild on plan changes**

In the constructor (~line 501), after the other subscriptions:

```csharp
            _simBriefService.PropertyChanged += SimBriefServiceOnPropertyChanged;
```

Add the handler and rebuild method (near the other service handlers):

```csharp
        private void SimBriefServiceOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BusinessLogic.SimBriefService.SimBriefService.MatchedFlightPlan)) return;
            // The service raises from background fetch tasks — marshal to the UI thread.
            System.Windows.Application.Current.Dispatcher.Invoke(UpdatePlannedRoute);
        }

        private void UpdatePlannedRoute()
        {
            PlannedRouteLocations.Clear();
            PlannedWaypoints.Clear();
            var plan = _simBriefService.MatchedFlightPlan;
            if (plan != null)
            {
                foreach (var point in plan.Points.OrderBy(p => p.Sequence))
                {
                    var location = new Location(point.Latitude, point.Longitude);
                    PlannedRouteLocations.Add(location);
                    if (point.Type == "apt") continue; // airports are already drawn on the map
                    PlannedWaypoints.Add(new PlannedWaypoint
                    {
                        Location = location,
                        Ident = point.Ident,
                        Tooltip = point.TooltipText
                    });
                }
                IsShowFlightPlan = true; // a freshly matched plan starts visible
            }
            OnPropertyChanged(nameof(IsFlightPlanAvailable));
            OnPropertyChanged(nameof(IsShowFlightPlanOverlay));
        }
```

- [ ] **Step 4: Add the overlay elements to `LiveView.xaml`**

Inside the map, directly before `<map:MapItemsControl ItemsSource="{Binding IvaoAtcList}"...>` (~line 75), add:

```xml
            <!-- SimBrief planned route overlay -->
            <map:MapPolyline Locations="{Binding PlannedRouteLocations}"
                             Stroke="{DynamicResource PlannedPathColorBrush}" StrokeThickness="2"
                             StrokeDashArray="4 2" Opacity="0.8"
                             Visibility="{Binding IsShowFlightPlanOverlay, Converter={StaticResource BoolToVis}}"/>
            <map:MapItemsControl ItemsSource="{Binding PlannedWaypoints}"
                                 Visibility="{Binding IsShowFlightPlanOverlay, Converter={StaticResource BoolToVis}}">
                <map:MapItemsControl.ItemContainerStyle>
                    <Style TargetType="map:MapItem">
                        <Setter Property="Location" Value="{Binding Location}"/>
                    </Style>
                </map:MapItemsControl.ItemContainerStyle>
                <map:MapItemsControl.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal" ToolTip="{Binding Tooltip}" ToolTipService.ShowDuration="10000">
                            <Ellipse Width="8" Height="8" Fill="{DynamicResource PlannedPathColorBrush}" Stroke="White" StrokeThickness="1"/>
                            <TextBlock Text="{Binding Ident}" Margin="4,0,0,0" FontSize="11"
                                       FontFamily="{DynamicResource CurrentFont}"
                                       Foreground="{DynamicResource SuperBrightTextColor}"/>
                        </StackPanel>
                    </DataTemplate>
                </map:MapItemsControl.ItemTemplate>
            </map:MapItemsControl>
```

- [ ] **Step 5: Add the toggle button**

In the right-side button stack, after the ATC toggle (before `</StackPanel>` at ~line 449):

```xml
                <!-- SimBrief plan overlay toggle - only exists when a matching plan was fetched -->
                <ToggleButton Style="{DynamicResource MapToggleButton}"
                              IsChecked="{Binding IsShowFlightPlan}"
                              Visibility="{Binding IsFlightPlanAvailable, Converter={StaticResource BoolToVis}}">
                    <TextBlock Foreground="{DynamicResource SuperBrightTextColor}">Plan</TextBlock>
                </ToggleButton>
```

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/ViewModels/LiveViewViewModel.cs FSTRaK/Views/LiveView.xaml FSTRaK/Resources/Theme.xaml FSTRaK/Resources/DarkTheme.xaml
git commit -m "feat: planned route overlay with waypoint labels on the live map"
```

---

### Task 7: Persist the plan at flight end + airline backfill

**Files:**
- Modify: `FSTRaK/BusinessLogic/FlightManager/State/FlightEndedState.cs`

**Interfaces:**
- Consumes: `SimBriefService.Instance.MatchedFlightPlan`, `SimBriefOfpMapper.ShouldSavePlan`, `Flight.FlightPlan`, `Aircraft.Airline`.
- Produces: plan rows persisted with the flight when eligible; `Aircraft.Airline` backfilled when blank.

- [ ] **Step 1: Add the attach method to `FlightEndedState.cs`**

```csharp
        /// <summary>
        /// Attaches the matched SimBrief plan when the flight landed at the planned arrival or one
        /// of the planned alternates. Never throws — plan persistence must not endanger the flight save.
        /// Must be called AFTER the aircraft is attached to the context so the airline backfill is
        /// detected as a modification.
        /// </summary>
        private void AttachFlightPlanIfEligible()
        {
            try
            {
                var plan = BusinessLogic.SimBriefService.SimBriefService.Instance.MatchedFlightPlan;
                if (plan == null)
                    return;

                if (!BusinessLogic.SimBriefService.SimBriefOfpMapper.ShouldSavePlan(plan, Context.ActiveFlight.ArrivalAirport))
                {
                    Log.Information($"SimBrief: not saving plan - arrival {Context.ActiveFlight.ArrivalAirport} matches neither planned arrival {plan.ArrivalAirport} nor alternates {plan.AlternateAirports}");
                    return;
                }

                Context.ActiveFlight.FlightPlan = plan;
                Log.Information($"SimBrief: plan {plan.ComposedFlightNumber} {plan.DepartureAirport} -> {plan.ArrivalAirport} attached to flight");

                if (string.IsNullOrWhiteSpace(Context.ActiveFlight.Aircraft?.Airline)
                    && !string.IsNullOrWhiteSpace(plan.AirlineIcao))
                {
                    Context.ActiveFlight.Aircraft.Airline = plan.AirlineIcao;
                    Log.Information($"SimBrief: backfilled blank aircraft airline with {plan.AirlineIcao}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SimBrief: failed to attach flight plan - saving flight without it");
            }
        }
```

Note on the state's namespace: `FlightEndedState` lives in `FSTRaK.BusinessLogic.FlightManager.State`, so the qualified name resolves as written above via the shared `FSTRaK.BusinessLogic` root — if the compiler resolves `BusinessLogic` against the current namespace and complains, use the fully qualified `FSTRaK.BusinessLogic.SimBriefService.SimBriefService.Instance` / `FSTRaK.BusinessLogic.SimBriefService.SimBriefOfpMapper` forms instead.

- [ ] **Step 2: Call it inside `SaveFlight()`** — between the aircraft attach block and `logbookContext.Flights.Add(...)` (order matters: the airline backfill must happen after `Attach` so EF detects the change):

```csharp
                        // Aircraft is potentially already in the db, so we attach it in this dbcontext. If the aircraft was pulled from the db in the flightstarted phase, it will have an ID.
                        if (Context.ActiveFlight.Aircraft.Id != 0)
                        {
                            logbookContext.Aircraft.Attach(Context.ActiveFlight.Aircraft);
                        }

                        AttachFlightPlanIfEligible();

                        logbookContext.Flights.Add(Context.ActiveFlight);
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/BusinessLogic/FlightManager/State/FlightEndedState.cs
git commit -m "feat: persist SimBrief plan with flight; backfill blank aircraft airline"
```

---

### Task 8: Logbook list — flight number and airline fallback

**Files:**
- Modify: `FSTRaK/ViewModels/LogbookViewModel.cs`
- Modify: `FSTRaK/Views/LogbookView.xaml`

**Interfaces:**
- Consumes: `Flight.PlanFlightNumber`, `Flight.DisplayAirline` (Task 1), `LogbookContext.FlightPlanPoints`.
- Produces: plan header loaded with each flight; plan points loaded on selection (Task 9 consumes `Flight.FlightPlan.Points`).

- [ ] **Step 1: Include the plan header in the flight list query**

In `LogbookViewModel.cs` (~line 295), extend the query:

```csharp
                            .Include(f => f.Aircraft)
                            .Include(f => f.FlightPlan);
```

(The `Points` collection is intentionally NOT included here — it is loaded on selection only.)

- [ ] **Step 2: Load plan points when a flight is selected**

In the selection handler where `FlightEvents` are loaded from the DB (inside the same `using (var logbookContext = new LogbookContext())` block, after `flight.FlightEvents = flightEvents;` ~line 90), add:

```csharp
                                if (flight.FlightPlan != null && (flight.FlightPlan.Points?.Count ?? 0) == 0)
                                {
                                    flight.FlightPlan.Points = logbookContext.FlightPlanPoints
                                        .Where(p => p.FlightPlanId == flight.Id)
                                        .OrderBy(p => p.Sequence)
                                        .ToList();
                                }
```

- [ ] **Step 3: Show flight number and airline fallback in the list row**

In `LogbookView.xaml` (~line 156), replace:

```xml
                            <Run Text="{Binding Path=Aircraft.Airline}"/>
```

with:

```xml
                            <Run Text="{Binding DisplayAirline, Mode=OneWay}"/> <Run Text="{Binding PlanFlightNumber, Mode=OneWay}"/>
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/ViewModels/LogbookViewModel.cs FSTRaK/Views/LogbookView.xaml
git commit -m "feat: show plan flight number and airline fallback in logbook list"
```

---

### Task 9: Flight details — replay overlay and planned-vs-actual card

**Files:**
- Modify: `FSTRaK/ViewModels/FlightDetailsViewModel.cs`
- Modify: `FSTRaK/Views/FlightDetailsView.xaml`
- Modify: `FSTRaK/ViewModels/FlightDetailsParamsViewModel.cs`
- Modify: `FSTRaK/Views/FlightDetailsParamsView.xaml`

**Interfaces:**
- Consumes: `Flight.FlightPlan` (+ `.Points`, loaded by Task 8 on selection), `FlightPlanPoint.TooltipText`, `FlightPlan.ComposedFlightNumber`/`AlternateList`, `Consts.LbsToKgs`, `Units` enum.
- Produces: replay overlay + "Plan" toggle; `FlightDetailsParamsViewModel.HasFlightPlan` / `FlightPlanText` (preformatted planned-vs-actual text incl. DIVERTED line).

- [ ] **Step 1: Add plan collections to `FlightDetailsViewModel.cs`**

Near `FlightPath` (~line 152):

```csharp
        public ObservableCollection<Location> PlannedRoutePath { get; } = new ObservableCollection<Location>();
        public ObservableCollection<PlannedWaypointPin> PlannedWaypoints { get; } = new ObservableCollection<PlannedWaypointPin>();

        public bool HasFlightPlan => PlannedRoutePath.Count > 0;

        private bool _isShowPlan = true;
        public bool IsShowPlan
        {
            get { return _isShowPlan; }
            set
            {
                _isShowPlan = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShowPlanOverlay));
            }
        }

        public bool IsShowPlanOverlay => HasFlightPlan && IsShowPlan;

        public class PlannedWaypointPin
        {
            public Location Location { get; set; }
            public string Ident { get; set; }
            public string Tooltip { get; set; }
        }
```

- [ ] **Step 2: Populate in `OnFlightEventsLoaded()`** — after the `FlightPath` foreach loop, add:

```csharp
            PlannedRoutePath.Clear();
            PlannedWaypoints.Clear();
            if (_flight.FlightPlan != null)
            {
                foreach (var point in _flight.FlightPlan.Points.OrderBy(p => p.Sequence))
                {
                    var location = new Location(point.Latitude, point.Longitude);
                    PlannedRoutePath.Add(location);
                    if (point.Type == "apt") continue; // airports are already visible on the map
                    PlannedWaypoints.Add(new PlannedWaypointPin
                    {
                        Location = location,
                        Ident = point.Ident,
                        Tooltip = point.TooltipText
                    });
                }
            }
            OnPropertyChanged(nameof(HasFlightPlan));
            OnPropertyChanged(nameof(IsShowPlanOverlay));
```

Also clear them in the `Flight` setter's stale-data branch (where `FlightPath.Clear(); MarkerList.Clear();` runs), adding:

```csharp
                    PlannedRoutePath.Clear();
                    PlannedWaypoints.Clear();
                    OnPropertyChanged(nameof(HasFlightPlan));
                    OnPropertyChanged(nameof(IsShowPlanOverlay));
```

- [ ] **Step 3: Add the overlay and toggle to `FlightDetailsView.xaml`**

After the existing `FlightPath` polyline (ends ~line 64), add:

```xml
                <!-- SimBrief planned route overlay -->
                <map:MapPolyline Locations="{Binding PlannedRoutePath}"
                                 Stroke="{DynamicResource PlannedPathColorBrush}" StrokeThickness="2"
                                 StrokeDashArray="4 2" Opacity="0.8"
                                 Visibility="{Binding IsShowPlanOverlay, Converter={StaticResource BoolToVis}}"/>
                <map:MapItemsControl ItemsSource="{Binding PlannedWaypoints}"
                                     Visibility="{Binding IsShowPlanOverlay, Converter={StaticResource BoolToVis}}">
                    <map:MapItemsControl.ItemContainerStyle>
                        <Style TargetType="map:MapItem">
                            <Setter Property="Location" Value="{Binding Location}"/>
                        </Style>
                    </map:MapItemsControl.ItemContainerStyle>
                    <map:MapItemsControl.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal" ToolTip="{Binding Tooltip}" ToolTipService.ShowDuration="10000">
                                <Ellipse Width="8" Height="8" Fill="{DynamicResource PlannedPathColorBrush}" Stroke="White" StrokeThickness="1"/>
                                <TextBlock Text="{Binding Ident}" Margin="4,0,0,0" FontSize="11"
                                           FontFamily="{DynamicResource CurrentFont}"
                                           Foreground="{DynamicResource SuperBrightTextColor}"/>
                            </StackPanel>
                        </DataTemplate>
                    </map:MapItemsControl.ItemTemplate>
                </map:MapItemsControl>
```

In the toggle stack (~line 87), after the Path toggle button, add:

```xml
                <ToggleButton Style="{DynamicResource MapToggleButton}"
                              IsChecked="{Binding IsShowPlan, Mode=TwoWay}"
                              Visibility="{Binding HasFlightPlan, Converter={StaticResource BoolToVis}}">
                    <TextBlock Foreground="{DynamicResource SuperBrightTextColor}">Plan</TextBlock>
                </ToggleButton>
```

- [ ] **Step 4: Add plan text to `FlightDetailsParamsViewModel.cs`**

Add properties:

```csharp
        public bool HasFlightPlan { get; }
        public string FlightPlanText { get; }
```

In the constructor, at the end:

```csharp
            HasFlightPlan = flight.FlightPlan != null;
            FlightPlanText = HasFlightPlan ? BuildFlightPlanText(flight) : string.Empty;
```

Add the builder (add `using System.Text;` and `using FSTRaK.Models.Entity;` is already present; also `using System;` is present):

```csharp
        private static string BuildFlightPlanText(Flight flight)
        {
            var plan = flight.FlightPlan;
            var isImperial = Properties.Settings.Default.Units == (int)Units.Imperial;
            var weightUnit = isImperial ? "Lbs" : "Kg";
            string Weight(double? lbs) =>
                lbs == null ? "N/A" : $"{(isImperial ? lbs.Value : lbs.Value * Consts.LbsToKgs):N0} {weightUnit}";
            string Duration(int? seconds) =>
                seconds == null ? "N/A" : TimeSpan.FromSeconds(seconds.Value).ToString(@"hh\:mm");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(plan.ComposedFlightNumber))
                sb.AppendLine($"Flight: {plan.ComposedFlightNumber}");
            sb.AppendLine($"Planned: {plan.DepartureAirport} -> {plan.ArrivalAirport} (Altn: {plan.AlternateAirports ?? "N/A"})");
            if (!string.Equals(plan.ArrivalAirport, flight.ArrivalAirport, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"DIVERTED to {flight.ArrivalAirport}");
            sb.AppendLine($"Aircraft: {plan.AircraftType} {plan.AircraftReg}");
            sb.AppendLine($"Route: {plan.Route}");
            if (plan.CruiseAltitude != null)
                sb.AppendLine($"Cruise altitude: {plan.CruiseAltitude} ft");
            sb.AppendLine($"Distance: planned {plan.RouteDistanceNm:N0} NM / flown {flight.FlightDistanceNm:N0} NM");
            sb.AppendLine($"Block time: planned {Duration(plan.EstBlockSec)} / actual {flight.FlightTime:hh\\:mm}");
            sb.AppendLine($"Fuel: ramp {Weight(plan.PlanRampFuel)}, planned burn {Weight(plan.EnrouteBurn)} / used {Weight(flight.TotalFuelUsed)}");
            sb.AppendLine($"Payload: planned {Weight(plan.PayloadLbs)} / actual {Weight(flight.TotalPayloadLbs)}");
            sb.Append($"Pax: {plan.PaxCount?.ToString() ?? "N/A"}, Bags: {plan.BagCount?.ToString() ?? "N/A"}, Cargo: {Weight(plan.CargoLbs)}");
            return sb.ToString();
        }
```

- [ ] **Step 5: Add the card to `FlightDetailsParamsView.xaml`**

Inside the root `StackPanel`, after the existing detail TextBlock (~line 87), add:

```xml
        <local:OverlayTextCardControl Header="Flight plan (SimBrief)" Text="{Binding FlightPlanText}"
                                      Visibility="{Binding HasFlightPlan, Converter={StaticResource BoolToVis}}"/>
```

If `BoolToVis` is not resolvable in this UserControl's scope (it is defined at App level for the other views — verify), add to its `UserControl.Resources`:

```xml
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
```

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/ViewModels/FlightDetailsViewModel.cs FSTRaK/Views/FlightDetailsView.xaml FSTRaK/ViewModels/FlightDetailsParamsViewModel.cs FSTRaK/Views/FlightDetailsParamsView.xaml
git commit -m "feat: plan overlay and planned-vs-actual card in flight details"
```

---

### Task 10: Version 3.7.0 and release documentation

**Files:**
- Modify: `FSTRaK/Properties/AssemblyInfo.cs`
- Modify: `Setup/Setup.vdproj`
- Modify: `RELEASE_NOTES.md`
- Modify: `docs/index.html`
- Modify: `docs/project-overview.md`
- Modify: `README.md`

- [ ] **Step 1: Bump assembly version** — in `AssemblyInfo.cs` lines 57-58:

```csharp
[assembly: AssemblyVersion("3.7.0.0")]
[assembly: AssemblyFileVersion("3.7.0.0")]
```

- [ ] **Step 2: Bump the MSI setup project** — in `Setup/Setup.vdproj` (~line 3978-3986):
  - `"ProductVersion" = "8:3.7.0"`
  - Replace the GUID inside `"ProductCode" = "8:{...}"` with a freshly generated uppercase GUID (`uuidgen | tr a-z A-Z`).
  - Replace the GUID inside `"PackageCode" = "8:{...}"` with a second freshly generated uppercase GUID.
  - **Do NOT change `UpgradeCode`** — it must stay `{DFB8349E-B81D-4481-9F4C-14C136E2FBD4}` so upgrades keep working.
  - Only touch the deployable-project section (~line 3978); the two earlier `"ProductCode" = "8:.NETFramework,Version=v4.7.2"` entries are unrelated launch-condition fields — leave them.

- [ ] **Step 3: Add the 3.7.0 entry to `RELEASE_NOTES.md`** — new section above 3.6.2, following its format:

```markdown
## 3.7.0

### SimBrief integration
- New setting: SimBrief username / Pilot ID (Settings → leave empty to disable).
- When a flight starts, FSTRaK silently fetches your latest SimBrief OFP and matches it against the detected departure airport (re-checked at taxi-out, which takes precedence).
- A "Plan" toggle on the live map overlays the planned route with waypoint labels and planned altitude/speed/fuel tooltips.
- When you land at the planned arrival or a planned alternate, the plan (aircraft, airports, route, fuel, times, weights, passengers/cargo and all navlog points) is saved with the flight.
- Flight details show the planned route overlay and a planned-vs-actual card (fuel, block time, distance, payload, pax/cargo), including a DIVERTED indicator when you landed at an alternate.
- The logbook shows the planned flight number (e.g. BAW0414); a blank aircraft airline is backfilled from the plan.
```

- [ ] **Step 4: Update the docs site (`docs/index.html`)** — GitHub Pages:
  - Change `<span class="release-version">3.6.2</span>` (~line 434) to `3.7.0`.
  - Read the surrounding release-notes/feature markup and add a matching 3.7.0 entry summarizing the SimBrief integration, following the existing structure exactly (there is a per-release notes block near the version span — mirror the 3.6.2 entry's markup).

- [ ] **Step 5: Update roadmaps**
  - `README.md` line 83: change `- [ ] Simbrief integration (fetch passengers, planned vs actual fuel and time, planned vs actual route).` to `- [x] ...` (checked).
  - `docs/project-overview.md` (~line 35): move/mark the SimBrief roadmap line as done, matching how previously-shipped items are represented in that file.

- [ ] **Step 6: Sync the csproj alias path** (required once after all csproj edits, see Global Constraints):

```bash
git add FSTRaK/Properties/AssemblyInfo.cs Setup/Setup.vdproj RELEASE_NOTES.md docs/index.html docs/project-overview.md README.md
git commit -m "chore: version 3.7.0 - release notes, docs site, roadmap updates"
git update-index --cacheinfo 100644,$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj),FSTRaK/FSTRaK.csproj
git commit -m "chore: sync FSTRaK.csproj alias with FSTrAk.csproj"
```

---

### Task 11: Final review pass

- [ ] **Step 1: Verify csproj completeness** — confirm `FSTRaK/FSTrAk.csproj` contains Compile entries for all five new files (`FlightPlan.cs`, `FlightPlanPoint.cs`, `SimBriefOfp.cs`, `SimBriefOfpMapper.cs`, `SimBriefService.cs`). A missing entry compiles locally-invisible but fails at runtime/CI.
- [ ] **Step 2: Verify no stray references** — `grep -rn "SimbriefUserId" FSTRaK/` must return nothing (the setting is `SimbriefUser`).
- [ ] **Step 3: Ask the user to push and open a PR** — CI runs the unit tests. Do not push yourself.

## Manual verification checklist (user, on Windows)

1. Empty SimBrief setting → no SimBrief log lines, no Plan button.
2. Generate an OFP, start a flight at its departure → Plan button appears, dashed route + labeled waypoints render, tooltips show planned alt/IAS/fuel/time.
3. Start the flight first, generate the OFP, then start taxiing → plan picked up at taxi-out.
4. Regenerate the OFP (different route) between start and taxi-out → taxi-out version wins.
5. OFP for a different departure → ignored (log line), no button.
6. Land at planned arrival, park, engines off → flight saved with plan; logbook row shows flight number; details show overlay + plan card.
7. Divert to the alternate → plan saved, DIVERTED line in the plan card.
8. Land somewhere else → flight saved without plan rows.
9. SimBrief account in kgs → stored/displayed values correct in both unit settings.
10. Aircraft with blank airline → airline backfilled after a plan-attached flight; non-blank airline untouched.
11. Old flights (no plan) → logbook and details unchanged.
12. Delete a flight that has a plan → no orphaned FlightPlan/FlightPlanPoint rows (check DB or re-open app without errors).
