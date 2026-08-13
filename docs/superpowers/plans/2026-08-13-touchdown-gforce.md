# Touchdown G-Force Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record peak touchdown G-force on every landing, persist it, show it in the replay popup and flight details, and fold it into the landing score via a worst-of rule against the FPM delta.

**Architecture:** Three new SimVars (`G FORCE`, `PLANE PITCH DEGREES`, `PLANE BANK DEGREES`) flow through the existing `FlightData` struct. `LandedState` tracks max G for 2 s after touchdown, then writes it onto the `LandingEvent` created at touchdown and recomputes `ScoreDelta`/`LandingRate` as the worse of the FPM and G ladders. A nullable `TouchdownGForce` column is added via EF6 automatic migration.

**Tech Stack:** .NET Framework 4.7.2 / C# 7.3, WPF MVVM, SimConnect, EF6 + SQLite.

**Spec:** `docs/superpowers/specs/2026-08-13-touchdown-gforce-design.md`

## Global Constraints

- **No build or tests on this Mac.** The repo has no automated test infrastructure; MSBuild only runs on the user's Windows machine. Verification steps are careful re-reads of the diff; end-to-end verification is the manual checklist in Task 5, run by the user on Windows.
- C# 7.3 only — no switch expressions, no `??=`, tuples OK.
- `FlightData` struct field order MUST exactly match the `AddToDataDefinition` call order — new fields and new definitions both go at the END.
- EF6 automatic migrations: additive nullable columns only; never rename/alter existing columns.
- Work on branch `feature/touchdown-g-force`. Never push without an explicit user request.
- Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: New SimVars — struct fields and data definitions

**Files:**
- Modify: `FSTRaK/DataTypes/SimConnectDataTypes.cs` (FlightData struct, after `Throttle4Position` ~line 155)
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs` (after the `GENERAL ENG THROTTLE LEVER POSITION:4` definition ~line 423, before `RegisterDataDefineStruct`)

**Interfaces:**
- Produces: `FlightData.GForce`, `FlightData.PitchDegrees`, `FlightData.BankDegrees` (all `double`) — consumed by Task 3.

- [ ] **Step 1: Add struct fields**

In `SimConnectDataTypes.cs`, inside `public struct FlightData`, immediately after `public double Throttle4Position;` and before the `MaxEngineRpmPct()` method:

```csharp
        public double GForce;
        public double PitchDegrees;
        public double BankDegrees;
```

- [ ] **Step 2: Add data definitions**

In `SimConnectService.cs`, immediately after the `GENERAL ENG THROTTLE LEVER POSITION:4` `AddToDataDefinition` call and before the blank line preceding `_simconnect.RegisterDataDefineStruct<AircraftData>(...)`:

```csharp
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "G FORCE", "GForce",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "PLANE PITCH DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "PLANE BANK DEGREES", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
```

- [ ] **Step 3: Verify ordering**

Re-read both edits: the three new struct fields must be the LAST data fields in the struct (methods after them are fine), and the three new `AddToDataDefinition` calls must be the LAST definitions for `DataDefinitions.FlightData`, in the SAME order (GForce, Pitch, Bank).

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/DataTypes/SimConnectDataTypes.cs FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs
git commit -m "feat: read G force, pitch and bank SimVars into FlightData

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: LandingEvent entity — TouchdownGForce column and popup text

**Files:**
- Modify: `FSTRaK/Models/Entity/FlightEvent/LandingEvent.cs`

**Interfaces:**
- Produces: `LandingEvent.TouchdownGForce` (`double?`, EF-mapped nullable column) — consumed by Tasks 3 and 4. EF6 automatic migration adds the column on next `LogbookContext` use; no manual migration code.

- [ ] **Step 1: Add the property and extend ToString**

Replace the full body of `LandingEvent.cs` with:

```csharp
using FSTRaK.DataTypes;
using System.ComponentModel.DataAnnotations.Schema;


namespace FSTRaK.Models
{
    internal class LandingEvent : ScoringEvent
    {
        [Column("FlapsPosition")]
        public double FlapsPosition { get; set; }
        public double VerticalSpeed { get; set; }


        public LandingRate LandingRate { get; set; }

        public double TouchDownPitchDegrees { get; set; }
        public double TouchDownBankDegress { get; set; }

        public double? TouchdownGForce { get; set; }

        [NotMapped] public override string EventName { get; set; } = "Landing";

        public override string ToString()
        {
            var text = $"{LandingRate}\n" + base.ToString() + $"\n{VerticalSpeed:F0} fpm";
            if (TouchdownGForce != null)
            {
                text += $"\n{TouchdownGForce:F2} G";
            }
            return text;
        }

    }
}
```

(Only two changes vs. current file: the `TouchdownGForce` property and the `ToString` body. `TouchDownBankDegress` misspelling is the existing column name — do NOT fix it.)

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/Models/Entity/FlightEvent/LandingEvent.cs
git commit -m "feat: add nullable TouchdownGForce to LandingEvent and show it in the event popup

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: LandedState — pitch/bank capture, max-G window, worst-of scoring

**Files:**
- Modify: `FSTRaK/BusinessLogic/FlightManager/State/LandedState.cs`

**Interfaces:**
- Consumes: `FlightData.GForce/PitchDegrees/BankDegrees` (Task 1), `LandingEvent.TouchdownGForce` (Task 2).
- Produces: persisted `LandingEvent` with `TouchdownGForce`, pitch/bank, and worst-of `ScoreDelta`/`LandingRate` — consumed by the existing `Flight.UpdateScore()`/`GetScoreDetails()` (no changes there) and by Task 4's UI.

- [ ] **Step 1: Rewrite LandedState.cs**

Replace the full file with:

```csharp
using System;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class LandedState : AbstractState
    {
        private const int GForceWindowMs = 2000;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private readonly LandingEvent _landingEvent;
        private readonly System.Diagnostics.Stopwatch _touchdownStopwatch = new System.Diagnostics.Stopwatch();
        private double _maxGForce;
        private bool _gForceFinalized;

        public LandedState(FlightManager context, FlightData landingData) : base(context)
        {
            this.EventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

            _landingEvent = ProcessLandingData(landingData, context);
            _maxGForce = landingData.GForce;
            _touchdownStopwatch.Start();
        }

        private LandingEvent ProcessLandingData(FlightData landingData, FlightManager context)
        {
            var le = new LandingEvent()
            {
                VerticalSpeed = landingData.VerticalSpeed,
                TouchDownPitchDegrees = landingData.PitchDegrees,
                TouchDownBankDegress = landingData.BankDegrees
            };

            if (landingData.VerticalSpeed < -500)
            {
                le.LandingRate = LandingRate.Hard;
                le.ScoreDelta = -35;
            }
            else if (landingData.VerticalSpeed < -350)
            {
                le.LandingRate = LandingRate.Fair;
                le.ScoreDelta = -10;
            }
            else if (landingData.VerticalSpeed < -190)
            {
                le.LandingRate = LandingRate.Good;
            }
            else if (landingData.VerticalSpeed < -165)
            {   
                le.LandingRate = LandingRate.Perfect;
                le.ScoreDelta = +10;
            }
            else if (landingData.VerticalSpeed < -135)
            {
                le.LandingRate = LandingRate.Good;
            }
            else if (landingData.VerticalSpeed < -101)
            {
                le.LandingRate = LandingRate.Soft;
                le.ScoreDelta = -10;
            }

            Log.Information($"Landed! Flaps: {le.FlapsPosition}, VS: {le.VerticalSpeed:F0} fpm, with {landingData.FuelWeightLbs} Lbs of fuel.");


            AddFlightEvent(landingData, le);
            context.ActiveFlight.LandingFpm = le.VerticalSpeed;

            return le;
        }

        public override void ProcessFlightData(FlightData data)
        {
            if (!_gForceFinalized)
            {
                if (data.GForce > _maxGForce)
                {
                    _maxGForce = data.GForce;
                }

                if (_touchdownStopwatch.ElapsedMilliseconds >= GForceWindowMs)
                {
                    FinalizeTouchdownGForce();
                }
            }

            if (!Convert.ToBoolean(data.SimOnGround))
            {
                FinalizeTouchdownGForce();
                Context.State = new FlightState(Context);
                return;
            }

            if (data.GroundVelocity < 35 && data.MaxThrottlePosition() < 50)
            {
                FinalizeTouchdownGForce();
                AddFlightEvent(data, new TaxiInEvent());
                Context.State = new TaxiInState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                AddFlightEvent(data, new BaseFlightEvent());
                Stopwatch.Restart();
            }
        }

        public override void HandleFlightExitEvent()
        {
            FinalizeTouchdownGForce();
            base.HandleFlightExitEvent();
        }

        /// <summary>
        /// Closes the post-touchdown max-G window: stores the peak G on the landing event
        /// and applies the worst (lowest) of the FPM-based and G-based score deltas.
        /// The rating label follows whichever delta is worse; ties keep the FPM rating.
        /// </summary>
        private void FinalizeTouchdownGForce()
        {
            if (_gForceFinalized)
            {
                return;
            }
            _gForceFinalized = true;

            _landingEvent.TouchdownGForce = _maxGForce;

            LandingRate gRating;
            int gDelta;
            GetGForceRating(_maxGForce, out gRating, out gDelta);

            if (gDelta < _landingEvent.ScoreDelta)
            {
                _landingEvent.ScoreDelta = gDelta;
                _landingEvent.LandingRate = gRating;
            }

            Log.Information($"Touchdown G: {_maxGForce:F2}, landing scored as {_landingEvent.LandingRate} ({_landingEvent.ScoreDelta} points).");
        }

        private static void GetGForceRating(double gForce, out LandingRate rating, out int scoreDelta)
        {
            if (gForce < 1.10)
            {
                rating = LandingRate.Soft;
                scoreDelta = -10;
            }
            else if (gForce < 1.15)
            {
                rating = LandingRate.Fair;
                scoreDelta = -10;
            }
            else if (gForce < 1.20)
            {
                rating = LandingRate.Good;
                scoreDelta = 0;
            }
            else if (gForce < 1.25)
            {
                rating = LandingRate.Perfect;
                scoreDelta = +10;
            }
            else if (gForce < 1.35)
            {
                rating = LandingRate.Good;
                scoreDelta = 0;
            }
            else if (gForce < 1.50)
            {
                rating = LandingRate.Fair;
                scoreDelta = -10;
            }
            else
            {
                rating = LandingRate.Hard;
                scoreDelta = -35;
            }
        }
    }
}
```

- [ ] **Step 2: Verify the worst-of semantics against the spec table**

Walk the cases by hand and confirm the code produces:
- FPM Perfect (+10) + G 1.22 (Perfect +10) → ScoreDelta +10, label Perfect (gDelta not < fpmDelta).
- FPM Perfect (+10) + G 1.30 (Good 0) → 0 / Good.
- FPM Perfect (+10) + G 1.55 (Hard −35) → −35 / Hard.
- FPM Fair (−10) + G 1.05 (Soft −10) → −10 / Fair (tie keeps FPM label).
- FPM Hard (−35) + G 1.20 (Perfect +10) → −35 / Hard.
- Bounce within 2 s: `SimOnGround == 0` path finalizes with the max captured so far; the next touchdown creates a fresh `LandedState`/`LandingEvent`.
- Sim exit within 2 s: `HandleFlightExitEvent` finalizes before `FlightEndedState` saves and calls `UpdateScore()`.

Note the FPM ladder falls through with `ScoreDelta = 0` and `LandingRate.NotSet` default for VS ≥ −101 fpm — unchanged behavior; the G rating then applies whenever `gDelta < 0`.

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/BusinessLogic/FlightManager/State/LandedState.cs
git commit -m "feat: capture peak touchdown G force and score landings as worst of FPM and G

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Flight details UI

**Files:**
- Modify: `FSTRaK/ViewModels/FlightDetailsParamsViewModel.cs`
- Modify: `FSTRaK/Views/FlightDetailsParamsView.xaml` (~line 73)

**Interfaces:**
- Consumes: `LandingEvent.TouchdownGForce` (Task 2).
- Produces: `FlightDetailsParamsViewModel.TouchdownGForce` (`string`, `"—"` when null) bound in the details text block.

- [ ] **Step 1: Add the view-model property**

In `FlightDetailsParamsViewModel.cs`, after `public double LandingVerticalSpeed { get; set; }` (line 48):

```csharp
        public string TouchdownGForce { get; set; }
```

In the constructor, after `LandingVerticalSpeed = CalculateLandingVs(flight);` (line 68):

```csharp
            TouchdownGForce = CalculateTouchdownGForce(flight);
```

After the `CalculateLandingVs` method (line 86), add:

```csharp
        private string CalculateTouchdownGForce(Flight flight)
        {
            var landingEvent = (LandingEvent)flight.FlightEvents.FirstOrDefault(e => e is LandingEvent);
            if (landingEvent?.TouchdownGForce != null)
            {
                return $"{landingEvent.TouchdownGForce:F2} G";
            }

            return "—";
        }
```

- [ ] **Step 2: Bind it in the XAML**

In `FlightDetailsParamsView.xaml`, after the Landing VS line + its `<LineBreak/>` (lines 73–74):

```xml
                <Run Text="Touchdown G: "/><Run Text="{Binding TouchdownGForce}"/>
                <LineBreak/>
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/ViewModels/FlightDetailsParamsViewModel.cs FSTRaK/Views/FlightDetailsParamsView.xaml
git commit -m "feat: show touchdown G force in flight details

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: Manual verification on Windows (user-run)

**Files:** none — checklist for the user.

- [ ] **Step 1: Build** `Debug|x64` in Visual Studio — compiles clean.
- [ ] **Step 2: Migration** — launch against an existing DB (`%LOCALAPPDATA%\FSTRaK_DEBUG`); no startup errors; `FlightEvents` table gains `TouchdownGForce`.
- [ ] **Step 3: Legacy data** — open an old flight: details show `Touchdown G: —`; replay landing popup has no G line; score unchanged.
- [ ] **Step 4: New landing** — fly a circuit; log line `Touchdown G: x.xx`; popup shows `x.xx G`; details show it; pitch/bank columns populated in DB.
- [ ] **Step 5: Scoring** — force a firm touchdown with modest FPM (or vice versa); score details show the downgraded label with the worse delta (e.g. `Hard Landing -35 Points` on a ≥1.5 G slam).
- [ ] **Step 6: Bounce** — bounced landing produces two LandingEvents, each with its own G.
