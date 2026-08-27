# UI responsiveness under the SIM_FRAME subscription

Date: 2026-08-27
Status: Design approved, pending implementation plan

## Problem

On the unreleased 3.7.5 the UI processes user input sluggishly during flight. The window
does not freeze — it keeps painting and eventually answers clicks — but input latency rises
to the point of being unusable while airborne.

3.7.5 has not shipped, so the regression never reached users. This is a fix to unreleased
work, not a hotfix: no version bump, and the 3.7.5 notes are corrected rather than
supplemented.

## Root cause

Commit `02b36c6` replaced a 50ms timed request loop for flight data with a standing
subscription at `SIMCONNECT_PERIOD.SIM_FRAME` and `interval: 0`, meaning every physics
frame. Three facts combine:

1. **Delivery lands on the UI thread.** SimConnect signals via `WM_USER_SIMCONNECT`;
   `WndProc` (`SimConnectService.cs:958`) calls `ReceiveMessage()`, which dispatches
   synchronously into `Simconnect_OnRecvSimobjectData` on the WPF dispatcher thread.

2. **Each sample runs the full downstream chain.** Assigning `FlightData`
   (`SimConnectService.cs:240`) raises `PropertyChanged`, which `FlightManager`
   (`FlightManager.cs:180`) answers by running `State.ProcessFlightData`, allocating a
   `FlightParams` and a `FlightIdentitySnapshot`, and firing
   `OnPropertyChanged(nameof(ActiveFlight))` — invalidating every binding on the active
   flight, unconditionally.

3. **The rate is now unbounded.** SIM_FRAME follows the physics loop, commonly 60–120+ Hz
   versus the previous fixed 20 Hz. `SIMCONNECT_DATA_REQUEST_FLAG.CHANGED` does not
   throttle it in practice: with 35 fields including latitude, longitude, G force, pitch,
   bank and RPM, essentially every frame differs in some low-order bit.

The UI thread therefore performs 3–6x the per-sample work it was designed for, and user
input queues behind it. The symptom appears mid-flight specifically because that is when
every field is genuinely changing.

### Contributing factor: lock hold time

`SafeSimConnectCall` (`SimConnectService.cs:848`) holds `_simConnectLock` across the
delegate. On the `ReceiveMessage` path this means the UI thread holds the lock for the
entire synchronous dispatch — the property change, the state machine, and everything they
reach. This violates the invariant stated in the lock's own comment at
`SimConnectService.cs:46`: *"Critical sections must stay narrow — never hold this across a
property change, because those reach the FlightManager state machine, which calls back
into this service."*

Consequences: the camera timer (every 250ms) and the connection timer (every 10s) block on
a lock held at frame rate for a long critical section, and the UI thread pays an
acquire/release on every frame. Lock reentrancy prevents a deadlock, but the invariant the
comment relies on is not being maintained.

### Ruled out

Database work is not a contributor. `LogbookContext` access in `FlightManager.cs:294` and
`FlightEndedState.cs:146` is already wrapped in `Task.Run`.

## Constraints

- 20 Hz is sufficient for the UI. This is the rate 3.7.4 shipped with, across every
  released version before `02b36c6`, and was never reported as insufficient.
- The benefits of `02b36c6` are kept: the separate camera poll (so camera state keeps
  arriving while the sim is paused) and the standing subscription (no GPU-load jitter, no
  request-loop overhead).
- Landing accuracy must not regress. This is the app's headline feature.
- The state machine stays on the UI thread. The measured problem is notification volume,
  not state-machine cost; moving it off-thread would reopen every thread-safety question
  around `ActiveFlight` for no established benefit.

## Design

### Why the gate cannot go at the top of the handler

The obvious fix — drop samples arriving within 50ms of the last one — would silently
degrade landing scores. Two consumers need per-frame data:

- `TouchdownTracker.Update()` (`TouchdownTracker.cs:66`) is a peak detector
  (`if (data.GForce > _maxGForce)`). A firm touchdown's G peak lasts tens of milliseconds
  and can fall entirely within a discarded 50ms window.
- `FlightState.ProcessFlightData` (`FlightState.cs:40`) transitions on
  `data.SimOnGround == 1`. A bounce where the gear touches between two gates would be
  missed outright.

### The split

Separate the two consumers by rate. The gate governs *notification*, not data.

**Every frame, ungated, on the UI thread:**

- Assign the `_flightData` backing field directly, without raising `PropertyChanged`.
- Call the state machine directly.

Per sample this is a struct copy plus a handful of comparisons. `AddFlightEvent` is
already interval-gated at 5–20s inside the individual states, and persistence is already
off-thread. The state machine was never the expensive part.

**Gated to 20 Hz (50ms minimum spacing, measured by `Stopwatch`):**

- `OnPropertyChanged(nameof(FlightData))`, and therefore everything downstream: the
  `FlightParams` and `FlightIdentitySnapshot` allocations, `CurrentFlightParams`, and the
  `ActiveFlight` binding invalidation.

The UI binds to `CurrentFlightParams` (`LiveViewViewModel.cs:248` and following), not to
`FlightData` directly, so throttling the notification reaches exactly the intended
consumer.

### Channel separation

`FlightManager` currently learns about new data via `PropertyChanged`, so the state machine
and the UI ride one signal. Splitting the rates means splitting the channel:

- The state machine gets a direct call — `FlightManager.HandleFlightData(FlightData)` —
  alongside the existing `HandleCameraTick()`, which already established this pattern.
- `PropertyChanged` for `FlightData` becomes UI-only.

The `case nameof(SimConnectService.FlightData)` block in
`SimconnectService_OnPropertyChange` splits accordingly: `State.ProcessFlightData` moves to
`HandleFlightData`; the `FlightParams` / `LastKnownSnapshot` / `ActiveFlight` work stays on
the notification path.

### Identity-check exemption

`VerifyFlightIdentity` (`SimConnectService.cs:805`) compares against
`FlightManager.LastKnownSnapshot`, which is refreshed only on the notification path. A
gated-away notification would leave the snapshot stale and make the comparison unreliable.

When `_pendingIdentityCheck` is set, the notification must be raised regardless of the
gate, and the gate's timestamp reset. This runs once per reconnect, so it costs nothing.

The existing ordering requirement documented at `SimConnectService.cs:797` still holds:
read the snapshot before the assignment.

### Upstream throttle — considered and rejected

An earlier revision of this spec set the subscription's `interval` parameter (the 7th
argument of `RequestDataOnSimObject`, measured in frames under `SIM_FRAME`) from `0` to
`2`, halving wire traffic and marshalling. It was implemented, then reverted during the
final whole-branch review. The interval stays at `0`: every frame is delivered.

Two reasons, and the first is the one that matters:

**It traded landing accuracy for an unmeasured optimization.** Delivering every other frame
halves the sample density feeding `TouchdownTracker`'s G-force peak detector and
`FlightState`'s `SimOnGround` bounce transition. A bounce is detected from a single
observation of `SimOnGround == 1`; halving the observations halves the chances of catching
a brief gear kiss. Landing scoring is this application's headline feature, and the gate —
not the interval — is what actually resolves the UI defect. Spending accuracy on a
marshalling saving nobody had measured was the wrong trade.

**At low frame rates it delivered fewer samples than the poll it replaced.** At 30fps,
`interval: 2` yields 15 Hz — below the 20 Hz of the pre-`02b36c6` loop this change is
restoring parity with. The earlier revision of this section claimed the opposite by
comparing against the poll's period rather than its rate.

It also made the release note in the task below false: that note states landing detection
sees every sample, which is true at `interval: 0` and false at `interval: 2`. Reverting
keeps code and documentation in agreement without rewording either.

### Narrowing the lock

Give `ReceiveMessage` a dedicated path that takes `_simConnectLock` only long enough to
read the handle into a local and confirm it is non-null, then calls `ReceiveMessage()`
outside the lock. Exception handling is unchanged.

All other callers keep `SafeSimConnectCall` as-is; those are genuinely narrow calls.

This trades in the possibility of `ReceiveMessage()` running against a handle being
disposed concurrently by `Close()`. That case is already covered: the
`ObjectDisposedException` / `NullReferenceException` catch at `SimConnectService.cs:867`
exists for it and triggers reconnection. No new failure mode is introduced.

## Testing

The existing suite (`FSTRaK.Tests`) covers pure logic — `FlightStateEvaluator`,
`FlightIdentity`, `ConnectionRecovery`. The gate is worth adding to that set, as it is pure
and the landing-accuracy argument rests on it:

- A notification gate admits the first sample, suppresses one arriving 10ms later, and
  admits one arriving 60ms later.
- The gate always admits when the identity-check flag is set, and resets its timestamp.

This requires extracting the gate as a small testable type (a `NotificationGate` with an
injectable clock) rather than an inline `Stopwatch`, consistent with how
`FlightStateEvaluator` and `ConnectionRecovery` were extracted in `02b36c6`'s parent
commits.

Not unit-testable here, per the project's constraints — verify manually on Windows:

1. UI stays responsive during cruise (the reported symptom).
2. Landing FPM and G values remain consistent with pre-3.7.5 flights for a comparable
   touchdown.
3. A bounced landing still merges into one landing event.
4. Leaving a flight while paused is still detected (the camera-poll path, untouched).
5. Reconnecting mid-flight still runs the identity check.

## Out of scope

- Moving the state machine off the UI thread.
- Reverting to the timed request loop.
- Any change to the camera poll.
