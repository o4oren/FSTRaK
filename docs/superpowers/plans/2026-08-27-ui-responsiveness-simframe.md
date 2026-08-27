# UI Responsiveness Under the SIM_FRAME Subscription — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore UI responsiveness during flight by throttling UI notification to 20Hz while keeping every frame flowing to the flight state machine, so landing accuracy is unaffected.

**Architecture:** Split one signal into two channels by rate. `SimConnectService` keeps assigning flight data every frame and calls the state machine directly, but raises `PropertyChanged` — which drives all UI binding work — only when 50ms have elapsed. A separate fix narrows `_simConnectLock` so `ReceiveMessage`'s synchronous dispatch no longer runs inside it.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, WPF, xUnit, Microsoft.FlightSimulator.SimConnect. Build is `x64` only.

**Spec:** `docs/superpowers/specs/2026-08-27-ui-responsiveness-simframe-design.md`

## Global Constraints

- **Cannot build or test on this machine.** The developer machine is macOS; MSBuild and the SimConnect native dependency are Windows-only. Every "run the tests" step below is a step the *user* runs on Windows. Do not claim a test passed that you did not see pass.
- **Both csproj files must be updated for any new file.** `FSTRaK/FSTrAk.csproj` is the real project; `FSTRaK/FSTRaK.csproj` is a tracked case-alias. A plain `git add` fails on the alias — stage it with `git update-index --add --cacheinfo` or `git add -f`, and verify with `git status` before committing.
- **Target framework is .NET Framework 4.7.2, with `<LangVersion>latest</LangVersion>`** in both `FSTRaK/FSTrAk.csproj` and `FSTRaK.Tests/FSTRaK.Tests.csproj`. Modern C# syntax compiles — the existing code uses `is not` patterns and nullable annotations (`FlightIdentitySnapshot?`). Match the surrounding file's style rather than writing to an older language level; do not "modernize" code you are only moving.
- **New extracted logic goes in `FSTRaK.BusinessLogic.SimconnectService`** as an `internal` type with a doc-comment explaining *why* it exists, following `ConnectionRecovery.cs` and `FlightStateEvaluator.cs`.
- **`InternalsVisibleTo("FSTRaK.Tests")` is already set** in `FSTRaK/Properties/AssemblyInfo.cs:8`, so `internal` types are directly testable.
- **Test project uses SDK-style globbing** — new test files need no csproj entry.
- **Do not push.** Ask the user before any push to remote.

---

### Task 1: Extract the notification gate as testable logic

The gate decides whether a flight-data sample should raise a UI property change. It is
extracted rather than written inline because the spec's landing-accuracy argument rests
on its exact behavior, and because an injectable clock makes it testable without timing
sleeps — consistent with how `FlightStateEvaluator` and `ConnectionRecovery` were
extracted.

**Files:**
- Create: `FSTRaK/BusinessLogic/SimconnectService/NotificationGate.cs`
- Create: `FSTRaK.Tests/NotificationGateTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj:137` (add `Compile Include` after `FlightStateEvaluator.cs`)
- Modify: `FSTRaK/FSTRaK.csproj:137` (same line, the case-alias)

**Interfaces:**
- Consumes: nothing.
- Produces: `internal sealed class NotificationGate`, constructed as
  `new NotificationGate(intervalMs: 50, clock: Func<long>)` and
  `new NotificationGate(intervalMs: 50)` for the production wall clock. One method:
  `bool ShouldNotify(bool force)`. Returns `true` and records the current time when the
  interval has elapsed or `force` is `true`; returns `false` otherwise. Task 2 calls it.

- [ ] **Step 1: Write the failing tests**

Create `FSTRaK.Tests/NotificationGateTests.cs`:

```csharp
using System;
using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class NotificationGateTests
    {
        // A hand-cranked clock: the tests advance time explicitly rather than sleeping,
        // so they stay fast and deterministic.
        private sealed class FakeClock
        {
            public long NowMs;
            public Func<long> Read => () => NowMs;
        }

        [Fact]
        public void ShouldNotify_FirstSample_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SampleInsideTheInterval_IsSuppressed()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;

            Assert.False(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SampleAfterTheInterval_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1060;

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_ExactlyAtTheInterval_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1050;

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_AdmittingResetsTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1060;
            Assert.True(gate.ShouldNotify(false));

            // 20ms past the sample that was just admitted, not 80ms past the first one.
            clock.NowMs = 1080;
            Assert.False(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SuppressingDoesNotResetTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            // A burst of suppressed samples must not push the next admission out.
            clock.NowMs = 1010;
            Assert.False(gate.ShouldNotify(false));
            clock.NowMs = 1030;
            Assert.False(gate.ShouldNotify(false));

            clock.NowMs = 1050;
            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_Forced_IsAdmittedInsideTheInterval()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;

            // The identity check after a reconnect must never be gated away.
            Assert.True(gate.ShouldNotify(true));
        }

        [Fact]
        public void ShouldNotify_Forced_ResetsTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;
            Assert.True(gate.ShouldNotify(true));

            clock.NowMs = 1040;
            Assert.False(gate.ShouldNotify(false));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

On Windows, in Visual Studio: **Test → Run All Tests**, or from a Developer Command Prompt:

```
vstest.console.exe FSTRaK.Tests\bin\x64\Debug\FSTRaK.Tests.dll /Tests:NotificationGateTests
```

Expected: compile error — `NotificationGate` does not exist. That is the correct failure.

- [ ] **Step 3: Write the implementation**

Create `FSTRaK/BusinessLogic/SimconnectService/NotificationGate.cs`:

```csharp
using System;
using System.Diagnostics;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Rate-limits UI property-change notifications for flight data.
    ///
    /// The flight data subscription runs at SIM_FRAME, which follows the simulator's
    /// physics loop - commonly 60-120+Hz. Raising a property change at that rate floods
    /// the UI thread with binding invalidation and makes the application unresponsive to
    /// input, which is the defect this class exists to prevent.
    ///
    /// Deliberately gates only the notification, never the data. The state machine still
    /// sees every sample, because TouchdownTracker's peak G detection and the airborne
    /// SimOnGround transition both degrade if frames are dropped: a firm touchdown's G
    /// peak lasts only tens of milliseconds and a bounce can fall entirely between two
    /// gate windows.
    /// </summary>
    internal sealed class NotificationGate
    {
        private readonly long _intervalMs;
        private readonly Func<long> _clock;

        private long _lastNotifiedMs;
        private bool _hasNotified;

        /// <summary>
        /// Uses a monotonic wall clock. Stopwatch rather than DateTime so that a system
        /// clock adjustment mid-flight cannot stall notifications.
        /// </summary>
        public NotificationGate(long intervalMs)
            : this(intervalMs, CreateStopwatchClock())
        {
        }

        /// <summary>
        /// Test seam: the clock reads milliseconds from an arbitrary origin, so tests can
        /// advance time explicitly rather than sleeping.
        /// </summary>
        public NotificationGate(long intervalMs, Func<long> clock)
        {
            _intervalMs = intervalMs;
            _clock = clock;
        }

        private static Func<long> CreateStopwatchClock()
        {
            var stopwatch = Stopwatch.StartNew();
            return () => stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// True when the caller should raise a property change. Records the time on every
        /// admission - including a forced one - so a burst of suppressed samples never
        /// pushes the next admission out.
        /// </summary>
        /// <param name="force">
        /// Admits regardless of the interval. Set for the post-reconnect identity check,
        /// which reads LastKnownSnapshot and would compare against stale data if the
        /// notification that refreshes it were gated away.
        /// </param>
        public bool ShouldNotify(bool force)
        {
            var now = _clock();

            if (!force && _hasNotified && now - _lastNotifiedMs < _intervalMs)
            {
                return false;
            }

            _lastNotifiedMs = now;
            _hasNotified = true;
            return true;
        }
    }
}
```

- [ ] **Step 4: Register the file in both csproj files**

In `FSTRaK/FSTrAk.csproj`, add after line 137 (`...FlightStateEvaluator.cs" />`):

```xml
    <Compile Include="BusinessLogic\SimconnectService\NotificationGate.cs" />
```

Make the identical edit at the identical position in `FSTRaK/FSTRaK.csproj`.

- [ ] **Step 5: Run the tests to verify they pass**

Same command as Step 2. Expected: all 8 `NotificationGateTests` pass.

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/NotificationGate.cs \
        FSTRaK.Tests/NotificationGateTests.cs \
        FSTRaK/FSTrAk.csproj
git add -f FSTRaK/FSTRaK.csproj
git status   # confirm BOTH csproj files are staged before committing
git commit -m "feat: extract the UI notification gate as pure logic

Gates only the notification, never the data: the state machine keeps
every frame so touchdown G peaks and bounces are not missed.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Split the flight-data channel by rate

Stop `FlightData`'s setter from notifying on every frame. The receive handler assigns the
field, calls the state machine directly, and raises the property change only when the gate
admits.

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs:238-249` (the `FlightData` property)
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs:799-816` (the receive handler)
- Modify: `FSTRaK/BusinessLogic/FlightManager/FlightManager.cs:180-211` (the `FlightData` case)

**Interfaces:**
- Consumes: `NotificationGate.ShouldNotify(bool force)` from Task 1.
- Produces: `public void HandleFlightData(FlightData data)` on `FlightManager`, called
  every frame from the receive handler. Task 3 does not depend on this.

- [ ] **Step 1: Add the gate field to SimConnectService**

Near the other timer/state fields around `SimConnectService.cs:51`, add:

```csharp
    /// <summary>
    /// Throttles UI notification for flight data to 20Hz - the rate that shipped before
    /// the SIM_FRAME subscription replaced the 50ms poll. The state machine is driven
    /// separately and still sees every frame.
    /// </summary>
    private readonly NotificationGate _flightDataNotificationGate = new NotificationGate(50);
```

- [ ] **Step 2: Make the FlightData setter silent**

Replace `SimConnectService.cs:238-249`:

```csharp
    private FlightData _flightData;

    public FlightData FlightData
    {
        get => _flightData;
        private set
        {
            _flightData = value;
            OnPropertyChanged();
        }
    }
```

with:

```csharp
    private FlightData _flightData;

    /// <summary>
    /// Assigning does NOT raise a property change - the setter is deliberately silent.
    /// Flight data arrives at simulator frame rate, and notifying at that rate is what
    /// makes the UI unresponsive. Simconnect_OnRecvSimobjectData raises the change
    /// through the notification gate instead.
    /// </summary>
    public FlightData FlightData
    {
        get => _flightData;
        private set => _flightData = value;
    }
```

- [ ] **Step 3: Drive the state machine and the gate from the receive handler**

Replace the `FlightDataRequest` branch at `SimConnectService.cs:799-816`:

```csharp
            if (data.dwRequestID == (int)Requests.FlightDataRequest)
            {
                // The snapshot has to be read before FlightData is assigned: that assignment
                // raises a property change the FlightManager answers by overwriting
                // LastKnownSnapshot with this very sample, which would make the comparison
                // below trivially true.
                var runIdentityCheck = _pendingIdentityCheck;
                FlightIdentitySnapshot? before = runIdentityCheck
                    ? FlightManager.FlightManager.Instance.LastKnownSnapshot
                    : null;

                FlightData = (FlightData)data.dwData[0];

                if (runIdentityCheck)
                {
                    _pendingIdentityCheck = false;
                    VerifyFlightIdentity(before, FlightData);
                }
            }
```

with:

```csharp
            if (data.dwRequestID == (int)Requests.FlightDataRequest)
            {
                // The snapshot has to be read before the notification below: that
                // notification is what makes the FlightManager overwrite LastKnownSnapshot
                // with this very sample, which would make the comparison trivially true.
                var runIdentityCheck = _pendingIdentityCheck;
                FlightIdentitySnapshot? before = runIdentityCheck
                    ? FlightManager.FlightManager.Instance.LastKnownSnapshot
                    : null;

                FlightData = (FlightData)data.dwData[0];

                // Every frame reaches the state machine. Touchdown G sampling and the
                // airborne transition both need per-frame resolution.
                FlightManager.FlightManager.Instance.HandleFlightData(_flightData);

                // The UI, which does not, is notified at 20Hz. Forced when an identity
                // check is pending, because that path depends on the notification having
                // refreshed LastKnownSnapshot.
                if (_flightDataNotificationGate.ShouldNotify(runIdentityCheck))
                {
                    OnPropertyChanged(nameof(FlightData));
                }

                if (runIdentityCheck)
                {
                    _pendingIdentityCheck = false;
                    VerifyFlightIdentity(before, FlightData);
                }
            }
```

- [ ] **Step 4: Move the state-machine call out of the property-change handler**

In `FSTRaK/BusinessLogic/FlightManager/FlightManager.cs`, replace the
`case nameof(SimConnectService.FlightData):` block at lines 180-211:

```csharp
                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;
                    State.ProcessFlightData(data);

                    // Updating the map in realtime if not in non-flight states
                    if (State is not SimNotInFlightState)
                    {
```

with (note only the first three lines change; the rest of the block is unchanged):

```csharp
                case nameof(SimConnectService.FlightData):
                    var data = _simConnectService.FlightData;

                    // State.ProcessFlightData is NOT called here - it runs on every frame
                    // via HandleFlightData. This path is the throttled UI half.

                    // Updating the map in realtime if not in non-flight states
                    if (State is not SimNotInFlightState)
                    {
```

- [ ] **Step 5: Add HandleFlightData to FlightManager**

Immediately above the existing `HandleCameraTick()` method (`FlightManager.cs:271`), add:

```csharp
        /// <summary>
        /// Driven directly by the flight data subscription rather than by a property
        /// change, so the state machine sees every frame while UI notification stays
        /// throttled. TouchdownTracker samples the peak G of a touchdown and FlightState
        /// watches for the aircraft leaving the ground; both lose accuracy if frames are
        /// dropped.
        /// </summary>
        public void HandleFlightData(FlightData data)
        {
            State.ProcessFlightData(data);
        }
```

- [ ] **Step 6: Document why delivery stays at every frame**

> **Superseded.** This step originally set the subscription's `interval` argument from
> `0u` to `2u` to halve marshalling. That was implemented and then reverted during the
> final whole-branch review: delivering every other frame halves the sample density
> feeding `TouchdownTracker`'s G-peak detector and `FlightState`'s bounce transition, and
> at 30fps it delivers below the 20 Hz of the poll this change restores parity with. The
> gate, not the interval, is what fixes the UI defect. See the spec's "Upstream throttle —
> considered and rejected" section.
>
> The `interval` argument stays `0u`. What remains of this step is the comment.

Leave the call itself as it is — every frame is delivered deliberately:

```csharp
        _simconnect.RequestDataOnSimObject(Requests.FlightDataRequest, DataDefinitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SIM_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);
```

Extend the comment directly above it so `0u` reads as a decision rather than an oversight:

```csharp
        // One standing subscription replaces the former 50ms request loop. SIM_FRAME is
        // tied to the physics loop rather than the render loop, so it does not fluctuate
        // with GPU load - and it stops while the simulator is paused, which is why camera
        // state is polled separately.
        //
        // interval 0 delivers every frame, deliberately. The state machine needs full
        // resolution: TouchdownTracker's peak G detection and the airborne SimOnGround
        // transition both degrade if frames are dropped. UI notification is throttled
        // separately, by the NotificationGate on the receive side, rather than by
        // reducing delivery here.
```

- [ ] **Step 7: Build and verify on Windows**

Build `Debug|x64` in Visual Studio. Expected: compiles clean. Run all tests — expected:
the 8 `NotificationGateTests` plus the existing `FlightStateEvaluatorTests`,
`FlightIdentityTests` and `ConnectionRecoveryTests` all pass.

Then fly a short circuit in MSFS and confirm:
1. The UI answers clicks promptly during cruise — the reported defect.
2. The live map still animates smoothly.
3. Landing FPM and G are consistent with a comparable flight logged under 3.7.4 (the last
   released version — 3.7.5 was never shipped, so the logbook has no flights recorded
   under the per-frame behavior).

- [ ] **Step 8: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs \
        FSTRaK/BusinessLogic/FlightManager/FlightManager.cs
git commit -m "fix: throttle UI notification for flight data to 20Hz

Flight data arrives at SIM_FRAME rate and every sample invalidated
every binding on the active flight, queueing user input behind it.

The state machine now runs off a direct per-frame call so touchdown
sampling is unchanged, while the property change - and the binding
work behind it - is gated back to 20Hz, the rate 3.7.4 shipped with.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Narrow the lock around ReceiveMessage

`SafeSimConnectCall` holds `_simConnectLock` across its delegate, so on the `ReceiveMessage`
path the UI thread holds the lock for the whole synchronous dispatch — the state machine
included. This violates the invariant documented at `SimConnectService.cs:46` and makes the
camera and connection timers contend with a lock held at frame rate.

Independent of Tasks 1-2; correct in either order.

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs:958-971` (`WndProc`)
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs` (add one method near `SafeSimConnectCall` at line 848)

**Interfaces:**
- Consumes: nothing.
- Produces: `private void ReceiveSimConnectMessage()`. Used only by `WndProc`.

- [ ] **Step 1: Add the narrow-lock receive path**

Directly below the existing `SafeSimConnectCall` method (which ends at
`SimConnectService.cs:872`), add:

```csharp
    /// <summary>
    /// Pumps SimConnect messages while holding the lock only long enough to read the
    /// handle.
    ///
    /// Deliberately does not use SafeSimConnectCall: ReceiveMessage dispatches its
    /// callbacks synchronously, so calling it inside the lock would hold the lock across
    /// the property change and the FlightManager state machine - exactly what the comment
    /// on _simConnectLock forbids, and enough to stall the camera and connection timers
    /// behind a lock taken at frame rate.
    ///
    /// Racing a concurrent Close is already handled: disposing the handle surfaces as an
    /// ObjectDisposedException or NullReferenceException, caught below, which reconnects.
    /// </summary>
    private void ReceiveSimConnectMessage()
    {
        SimConnect handle;
        lock (_simConnectLock)
        {
            handle = _simconnect;
        }

        if (handle == null)
        {
            Log.Debug("Skipping ReceiveMessage - no SimConnect handle.");
            return;
        }

        try
        {
            handle.ReceiveMessage();
        }
        catch (COMException ex)
        {
            HandleCOMException(ex);
        }
        catch (Exception ex) when (ex is NullReferenceException || ex is ObjectDisposedException)
        {
            Log.Warning(ex, "SimConnect handle disposed during ReceiveMessage; reconnecting.");
            HandleConnectionLost();
        }
    }
```

- [ ] **Step 2: Call it from WndProc**

In `SimConnectService.cs:967`, replace:

```csharp
            SafeSimConnectCall(sc => sc.ReceiveMessage(), "ReceiveMessage");
```

with:

```csharp
            ReceiveSimConnectMessage();
```

- [ ] **Step 3: Correct the lock's doc comment**

The comment at `SimConnectService.cs:43-49` names the three threads reaching the handle.
Replace its body so it describes what the code now does:

```csharp
    /// <summary>
    /// Guards every access to <see cref="_simconnect"/>. Three threads reach the handle:
    /// the UI thread via WndProc, the camera timer, and the connection timer. Critical
    /// sections must stay narrow - never hold this across a property change, because
    /// those reach the FlightManager state machine, which calls back into this service.
    /// The WndProc path therefore uses <see cref="ReceiveSimConnectMessage"/>, which
    /// takes this only to read the handle and pumps messages outside it.
    /// </summary>
```

- [ ] **Step 4: Build and verify on Windows**

Build `Debug|x64`. Expected: compiles clean; all tests still pass.

Verify the paths this touches, which are connection-lifecycle rather than in-flight:
1. Start FSTRaK before MSFS — it connects when the sim comes up.
2. Quit MSFS mid-flight — FSTRaK detects the drop and begins reconnecting rather than
   hanging or spinning.
3. Restart MSFS and rejoin — the connection recovers and the identity check runs.

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs
git commit -m "fix: pump SimConnect messages outside the handle lock

ReceiveMessage dispatches synchronously, so calling it through
SafeSimConnectCall held the lock across the state machine - what the
lock's own comment forbids - and stalled the camera and connection
timers behind a lock taken at frame rate.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Correct the unreleased 3.7.5 notes

**3.7.5 has not been released.** This regression never reached users, so there is no
version bump and no new release section — do not touch `AssemblyInfo.cs` or
`Setup/Setup.vdproj`. What is needed is a correction: the existing 3.7.5 notes describe the
SIM_FRAME change in terms this fix partly invalidates.

The claim at `RELEASE_NOTES.md:25` — flight data "now arrives on a SimConnect subscription
tied to the simulator's physics loop, instead of being polled 20 times a second" — will be
only half true. The state machine does consume every frame, so the landing-precision claim
survives; but the UI is deliberately back to 20Hz, which that sentence tells users is gone.

**Files:**
- Modify: `RELEASE_NOTES.md:25` (the first bullet under `## Changes`)
- Modify: `docs/index.html` (the matching 3.7.5 entry, only if it repeats the claim)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Correct the Changes bullet**

In `RELEASE_NOTES.md`, replace the first bullet under `## Changes`:

```markdown
- Flight data now arrives on a SimConnect subscription tied to the simulator's physics loop, instead of being polled 20 times a second. This reduces inter-process traffic and samples aircraft state more accurately, which slightly improves the precision of landing scores.
```

with:

```markdown
- Flight data now arrives on a SimConnect subscription tied to the simulator's physics loop, instead of being polled 20 times a second. Landing detection sees every sample, which slightly improves the precision of landing scores. The displayed flight data and live map continue to refresh 20 times a second, which is what they did before and is well past the point of visible difference.
```

- [ ] **Step 2: Check whether the docs site repeats the claim**

```bash
grep -n "physics loop\|20 times a second" docs/index.html
```

If the 3.7.5 entry repeats the original wording, apply the same correction there, matching
the surrounding HTML structure. If it does not mention it, leave the file alone — do not
invent an entry for a fix users will never see as separate.

- [ ] **Step 3: Commit**

```bash
git add RELEASE_NOTES.md
# add docs/index.html only if Step 2 actually changed it
git commit -m "docs: correct the unreleased 3.7.5 note on flight data rate

The subscription feeds landing detection every frame, but the UI is
deliberately back to 20Hz - the note as written said that rate was
gone.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Verification Before Completion

Do not report this fix as working on the strength of a clean build. The defect is a
runtime, in-flight behavior, and the only evidence that counts is a flight on Windows:

- [ ] UI responds promptly to clicks during cruise — the original symptom.
- [ ] Live map animation is still smooth at 20Hz.
- [ ] Landing FPM and G match expectations for a comparable touchdown.
- [ ] A bounced landing still records as one landing event.
- [ ] Leaving a flight while the sim is paused is still detected.
- [ ] A mid-flight reconnect still runs the identity check.

If the latency is only partly improved, the diagnosis — that notification volume is the
dominant cost — is what to re-examine, rather than adding a second fix on top. The spec's
root-cause section was derived from reading the code, not from a profiler trace.
