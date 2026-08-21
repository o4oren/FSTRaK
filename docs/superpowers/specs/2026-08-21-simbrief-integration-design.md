# SimBrief Integration — Design

**Date:** 2026-08-21
**Status:** Approved design, pending implementation plan

## Summary

FSTRaK fetches the user's latest SimBrief OFP at the start of a flight, matches it
against the detected departure airport, lets the user overlay the planned route on the
live map, and — when the flight lands at the planned arrival or one of the planned
alternates — persists the plan alongside the flight for planned-vs-actual presentation
in the logbook.

The integration is silent: no plan, no SimBrief account, or any fetch/parse failure
simply means the feature does nothing. Failures are logged, never surfaced as UI errors.

## SimBrief API

- Endpoint: `https://www.simbrief.com/api/xml.fetcher.php?userid=<id>&json=1`
  (or `username=<name>` when the configured value is not all digits).
- No authentication. Returns the user's latest generated OFP.
- Error shape: `fetch.status` contains `"Error: ..."` (e.g. "No flight plan on file");
  success is `fetch.status == "Success"`.
- `params.units` is `"kgs"` or `"lbs"` — all weights/fuel in the response are in that
  unit and must be normalized to lbs on ingest (FSTRaK stores lbs internally).
- Times: `times.sched_out/est_out/...` are epoch seconds (UTC); `est_time_enroute`,
  `est_block` are duration seconds. Navlog `time_total` is seconds from takeoff.
- `alternate` may be a JSON object, a list of objects (multiple alternates), or empty.
- Consumed subtrees only: `fetch`, `params`, `general`, `origin`, `destination`,
  `alternate`, `aircraft`, `fuel`, `times`, `weights`, `navlog.fix[]`.
- A real sample response (EGLL→ELLX, alternate EDFH, kgs units) was captured during
  design and becomes the unit-test fixture.

## Data Model

Two new EF6 entities in `Models/Entity/`, registered on `LogbookContext`. Automatic
migrations create the tables; existing flights simply have no plan row.

### `FlightPlan` — one-to-optional-one with `Flight`

EF6 shared-primary-key pattern: `FlightPlan.Id` is both PK and FK to `Flight.Id`,
configured with `HasOptional(f => f.FlightPlan).WithRequired(p => p.Flight)`.

All weights/fuel stored in **lbs** (converted from kgs at ingest when needed).

| Group    | Columns (SimBrief source) |
|----------|---------------------------|
| Identity | `Id` (PK/FK), `AirlineIcao`, `FlightNumber` (`general.icao_airline`, `general.flight_number`) |
| Aircraft | `AircraftType` (`aircraft.icaocode`), `AircraftName` (`aircraft.name`), `AircraftReg` (`aircraft.reg`) |
| Airports | `DepartureAirport` (`origin.icao_code`), `ArrivalAirport` (`destination.icao_code`), `AlternateAirports` (comma-joined `alternate[*].icao_code`) |
| Route    | `Route` (`general.route`), `CruiseAltitude` (`general.initial_altitude`), `RouteDistanceNm` (`general.route_distance`) |
| Fuel     | `TaxiFuel`, `EnrouteBurn`, `ContingencyFuel`, `AlternateFuel`, `ReserveFuel`, `ExtraFuel`, `PlanRampFuel`, `PlanTakeoffFuel`, `PlanLandingFuel` (`fuel.*`) |
| Times    | `ScheduledOut`, `ScheduledIn` (DateTime UTC from epoch `times.sched_out/sched_in`), `EstTimeEnrouteSec`, `EstBlockSec` |
| Payload  | `PaxCount`, `BagCount`, `CargoLbs`, `PayloadLbs`, `EstZfw`, `EstTow`, `EstLdw` (`weights.*`) |

### `FlightPlanPoint` — many-to-one with `FlightPlan`, ordered by `Sequence`

`Id` (PK), `FlightPlanId` (FK), `Sequence`, `Ident`, `Name`, `Type`, `ViaAirway`,
`Stage` (CLB/CRZ/DES), `IsSidStar`, `Latitude`, `Longitude` (`pos_lat`/`pos_long`),
`AltitudeFt` (`altitude_feet`), `IndicatedAirspeed` (`ind_airspeed`),
`FuelOnboardLbs` (`fuel_plan_onboard`), `TimeTotalSec` (`time_total`),
`DistanceNm` (`distance`).

Deliberately excluded (YAGNI): weather, winds, ETOPS, crew, TLR, per-fix FIR data.

## SimBriefService

New folder `BusinessLogic/SimBriefService/`:

- **`SimBriefService.cs`** — thread-safe singleton (double-checked locking, same shape
  as `VatsimService`). Subscribes to `FlightManager.Instance.PropertyChanged`. No
  timers, no polling — purely event-driven. Exposes `MatchedFlightPlan`
  (`FlightPlan` entity or null) via `INotifyPropertyChanged`.
- **`SimBriefModel/`** — DTO classes for the consumed JSON subtrees, deserialized with
  the same JSON library used by the VATSIM/IVAO services.
- **`SimBriefOfpMapper`** (pure, no HTTP/singleton/WPF dependencies) — DTO →
  `FlightPlan`/`FlightPlanPoint` mapping, unit normalization, epoch conversion,
  alternate dict-vs-list handling, and the matching predicates. This is the
  unit-tested surface; `SimBriefService` is a thin orchestrator around it.

### Fetch lifecycle

1. **Checkpoint 1 — flight started.** When the state becomes `FlightStartedState` and
   the flight's `DepartureAirport` is resolved: fetch the latest OFP. If
   `fetch.status == "Success"` and `origin.icao_code` equals the flight's departure
   ICAO (exact string match), map and publish `MatchedFlightPlan`. Purpose: overlay
   available while still parked.
2. **Checkpoint 2 — first movement.** On transition into `TaxiOutState` (or directly
   into `TakeoffRollState`): **always** fetch again. If the new OFP's departure
   matches, it **replaces** any checkpoint-1 plan — checkpoint 2 is the source of
   truth (covers regenerating the plan while boarding). If the fetch fails or the
   departure does not match, the checkpoint-1 plan (if any) is kept.
3. **Lock.** After checkpoint 2, no further fetches; the plan never changes mid-flight.
   `MatchedFlightPlan` resets to null on flight end / return to `SimNotInFlightState`.

If the SimBrief setting is empty, the service never fetches.

Fetches run on background tasks with a 10s timeout. Failures (network, non-success
status, malformed JSON) are caught and logged (Information for no-match/no-plan,
Warning/Error for network or parse failures) and leave the current state unchanged.
A plan that cannot be fully parsed is discarded, never half-saved.

## Live Map UI

- **"Plan" toggle button** in the existing right-side `MapToggleButton` stack in
  `LiveView.xaml`, text-labeled like the Pilots/ATC toggles. `Visibility` binds to a
  new `LiveViewViewModel.IsFlightPlanAvailable` (true iff
  `SimBriefService.MatchedFlightPlan != null`) — the button does not exist unless a
  relevant plan matched. `IsChecked` binds to `IsShowFlightPlan`; defaults **on** when
  a plan first matches; resets per flight.
- **Overlay**, visible only when available && toggled on:
  1. `MapPolyline` over `PlannedRouteLocations` (`ObservableCollection<Location>`
     built from plan points) — dashed stroke, color distinct from the actual-track
     palette, moderate opacity so the live track stays dominant.
  2. `MapItemsControl` over the plan points: small dot + `Ident` label per waypoint
     (TOC/TOD included; origin/destination airport fixes skipped — airports are
     already drawn). Tooltip per dot: planned altitude, IAS, fuel onboard, time.
- When checkpoint 2 replaces the plan, collections rebuild via `Dispatcher.Invoke`
  (same pattern as `UpdateFlightPathLines`; respects the known DependencyProperty
  cross-thread constraint). Collections clear and the button disappears on reset.
- No changes to map centering/zoom; the overlay is passive.

## Persistence at Flight End

Inside the existing `FlightEndedState` save path — a plan is only persisted if the
flight itself is persisted (completed, or "save only complete flights" disabled).

- **Save gate:** attach the plan iff `MatchedFlightPlan != null` and the actual
  arrival ICAO equals the plan's `ArrivalAirport` **or** one of its
  `AlternateAirports`. Landing anywhere else → plan not saved, discarded on reset.
- **Attach:** `flight.FlightPlan = plan` before `SaveFlight()` writes, so the
  `FlightPlan` row and its `FlightPlanPoint` children save in the same context as the
  flight (shared PK assigned by EF).
- **Diversion:** no stored flag. A diversion is derivable and precise:
  `FlightPlan.ArrivalAirport != Flight.ArrivalAirport` can only mean "landed at an
  alternate" given the save gate.
- **Airline backfill:** in the same save, if the plan is attached, its `AirlineIcao`
  is non-blank, and the flight's `Aircraft.Airline` is null/blank — set
  `Aircraft.Airline` to the plan's ICAO code (persists for future flights of that
  aircraft). Never overwrites a non-blank value.
- **Safety:** the plan attach is wrapped so any exception logs and the flight still
  saves without a plan. Plan persistence must never lose a flight.

## Logbook / Flight Details Presentation

All reads come from `flight.FlightPlan`; null (legacy or plan-less flights) collapses
everything below.

1. **Flight number** — composed `AirlineIcao + FlightNumber` (e.g. "BAW123") shown in
   the logbook list row / flight header when a plan exists. Airline display uses
   `Aircraft.Airline`, falling back to the plan's raw `AirlineIcao` when blank (no
   ICAO→name dataset; YAGNI).
2. **Replay map overlay** — same visual as the live map (dashed planned polyline +
   waypoint dots with ident labels and planned-detail tooltips) with the same "Plan"
   toggle in `FlightDetailsView`'s map, visible only when the flight has a plan.
3. **Planned vs actual panel** — a section in `FlightDetailsParamsView`, shown only
   when a plan exists, as compact planned / actual / delta rows:
   - Fuel: `PlanRampFuel` and `EnrouteBurn` vs actual `TotalFuelUsed`
   - Time: `EstBlockSec` vs `FlightTime`; `ScheduledOut`/`ScheduledIn` vs actual
     start/end
   - Payload: `PayloadLbs` vs `TotalPayloadLbs`; pax/bag/cargo shown planned-only
   - Route: `RouteDistanceNm` vs `FlightDistanceNm`; planned cruise altitude
   - Airports: planned dep/arr/alternates vs actual, with a **"Diverted"** badge when
     the saved plan's arrival differs from the flight's actual arrival
   - Values go through the existing unit-conversion utils to respect the user's units
     setting.

## Settings

One new `Properties.Settings` entry: `SimbriefUserId` (string, default empty). A
single textbox in `SettingsView` — "SimBrief username or pilot ID" — following the
existing settings textbox patterns. All-digits → `userid=`, otherwise `username=`.
Empty → feature fully dormant.

## Testing

### Unit tests (`FSTRaK.Tests`, xUnit, run by CI on PRs)

Target the pure `SimBriefOfpMapper` and helpers:

- **OFP parsing/mapping** using the captured EGLL→ELLX kgs fixture: field values,
  point ordering, epoch→DateTime conversion.
- **Unit normalization:** kgs fixture → lbs stored values; lbs passthrough case.
- **Matching rules:** departure match/mismatch; arrival save-gate (planned arrival ✓,
  each alternate ✓, anywhere else ✗); multiple alternates; `alternate` as object vs
  list vs empty.
- **Setting interpretation:** all-digits → `userid`, otherwise `username`, empty →
  dormant.

### Manual Windows checklist (integrated behavior)

- Empty setting → fully dormant (no requests in log).
- Plan matched at flight start → overlay button appears, overlay renders.
- Plan generated only after loading the sim → picked up at taxi-out (checkpoint 2).
- Plan regenerated between start and taxi-out → checkpoint 2 version wins.
- OFP with different departure → ignored, no button.
- Overlay toggle on live map and in flight-details replay.
- Landing at planned arrival → plan saved; planned-vs-actual panel renders.
- Diversion to alternate → plan saved, "Diverted" badge shown.
- Landing elsewhere → no plan rows written.
- kgs SimBrief account → values displayed correctly in the user's units.
- Aircraft with blank airline → backfilled from plan; non-blank never overwritten.
- Legacy flights (no plan) → logbook and details unaffected.

## Out of Scope

- Mid-flight OFP refresh or route-change tracking.
- Airline ICAO→name resolution dataset.
- Weather/wind/ETOPS/crew data from the OFP.
- Prefiling or any write operations to SimBrief.
