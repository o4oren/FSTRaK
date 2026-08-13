# Touchdown G-Force: Capture, Display, and Scoring

**Date:** 2026-08-13
**Status:** Approved

## Goal

Record the peak G-force at touchdown for every landing, persist it on the
`LandingEvent`, show it in the UI, and fold it into the flight score using a
worst-of rule against the existing FPM-based landing score.

## 1. SimVar and peak capture

- Add `G FORCE` (unit `GForce`, `SIMCONNECT_DATATYPE.FLOAT64`) to:
  - the `FlightData` struct in `FSTRaK/DataTypes/SimConnectDataTypes.cs`
  - the flight-data definition block in
    `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs` (~line 355).
  - Field order in the struct must match the AddToDataDefinition order.
- A single frame sample misses the spike (50 ms poll), and the session-wide
  `MAX G FORCE` SimVar is not per-landing. Instead, `LandedState` tracks the
  maximum observed `GForce` across samples for a **2-second window** starting
  at touchdown.
- The window finalizes when 2 s elapse **or** the state exits early (bounce
  back to `FlightState`, or deceleration into `TaxiInState`). On finalize, the
  max G is written to the `LandingEvent` created at touchdown and the score
  delta / rating are recomputed (see §3).
- A bounce that touches down again creates a new `LandingEvent` with its own
  window — same behavior as FPM today.

## 2. Freebie: pitch and bank at touchdown

`LandingEvent` already has never-populated `TouchDownPitchDegrees` and
`TouchDownBankDegress` columns (the "TODO future handling of pitch" in
`LandedState`). Populate both from the touchdown `FlightData` in the same pass.
Sign convention: store the raw values as reported by SimConnect (note
`PLANE PITCH DEGREES` is negative nose-up). No UI displays pitch/bank yet, so
any display-side negation is a future concern.

## 3. Database

- New nullable column `TouchdownGForce` (`double?`) on `LandingEvent`
  (TPH table `FlightEvents`).
- Additive `AddColumn` is supported by the SQLite EF6 automatic-migration
  generator (precedent: `LandingFpm`). No manual migration or `Seed` backfill.
- Old landings remain `null` → UI shows "—" and scoring is untouched
  (score is only computed once, at flight end).

## 4. Scoring — worst-of FPM/G

Current behavior: `LandedState` sets `LandingEvent.ScoreDelta` from FPM bands;
`Flight.UpdateScore()` (called once in `FlightEndedState`) computes
`Score = clamp(100 + Σ ScoreDelta, 0, 110)`.

New G ladder (max touchdown G → rating → delta):

| Max touchdown G | Rating  | G delta |
|-----------------|---------|---------|
| < 1.10          | Soft    | −10     |
| 1.10 – 1.14     | Fair    | −10     |
| 1.15 – 1.19     | Good    | 0       |
| 1.20 – 1.24     | Perfect | +10     |
| 1.25 – 1.34     | Good    | 0       |
| 1.35 – 1.49     | Fair    | −10     |
| ≥ 1.50          | Hard    | −35     |

Implemented as ascending threshold checks: `< 1.10 → Soft(−10)`,
`< 1.15 → Fair(−10)`, `< 1.20 → Good(0)`, `< 1.25 → Perfect(+10)`,
`< 1.35 → Good(0)`, `< 1.50 → Fair(−10)`, else `Hard(−35)`.

Worst-of rule, applied when the G window finalizes:

- `ScoreDelta = min(fpmDelta, gDelta)` — the worse delta wins; penalties are
  never summed, so one hard landing is not punished twice.
- The Perfect +10 survives only when **both** ladders award it
  (a natural consequence of `min`).
- `LandingRate` (the displayed label) also follows worst-of: the rating whose
  delta is worse. On equal deltas, keep the FPM-based rating.
- If G is unavailable (null), the FPM-only delta and rating stand.
- No `LandingRate` enum change needed (`Soft, Fair, Good, Perfect, Hard,
  NotSet` covers the ladder). No changes to `Flight.UpdateScore()` or
  `Flight.GetScoreDetails()` — the existing
  `"{LandingRate} Landing {delta} Points"` line stays consistent because
  label and delta are recomputed together.

## 5. UI

- **Map replay popup** — `LandingEvent.ToString()`: append a G line after the
  fpm line when `TouchdownGForce` is non-null, e.g. `1.42 G` (format `F2`).
  Null → omit the line entirely (legacy flights).
- **Flight details** — `FlightDetailsParamsViewModel`: expose a
  `TouchdownGForce` string property from the first `LandingEvent` (same
  event-selection pattern as `LandingVerticalSpeed`); `"—"` when null.
  Bind it in `FSTRaK/Views/FlightDetailsParamsView.xaml` next to the
  "Landing VS" run (line ~73).
- Logbook grid column: out of scope for this iteration.

## 6. Testing (manual, on Windows)

No automated test infrastructure exists in this repo; verification is manual:

1. Startup migration: launch the new build against an existing DB — no
   migration errors, `TouchdownGForce` column added.
2. Legacy data: open an old flight — details show "—", replay popup shows no
   G line, score unchanged.
3. New landing: fly a circuit; confirm logged max G looks sane, appears in
   popup + details, pitch/bank columns populated.
4. Scoring worst-of: force a firm/hard touchdown; score details line shows the
   downgraded rating and the worse delta (e.g. perfect FPM + 1.5 G →
   "Hard Landing −35 Points").
5. Bounce: a bounced landing produces two LandingEvents, each with its own G.

## Process

Feature branch off `main`; no push without explicit request; user tests on
Windows (no build possible on this Mac).
