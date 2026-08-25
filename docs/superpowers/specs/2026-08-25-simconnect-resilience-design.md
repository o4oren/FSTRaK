# SimConnect Resilience and Data Flow Redesign

**Date:** 2026-08-25
**Status:** Approved, pending implementation

## Problem

An external review raised five risks in `SimConnectService`. Four are real and are
addressed here; the fifth (diagnostic logging) is already adequate.

1. **Unhandled pipe disconnection.** `HandleCOMException` recovers only from
   `0xC000014B`. `0xC00000B0` (`STATUS_PIPE_DISCONNECTED`) falls through to
   `default:`, which logs and does nothing. The data timer keeps firing at 20 Hz
   against a dead pipe, `IsConnected` stays `true`, and the reconnect timer never
   starts — a permanent zombie state.
2. **Unsynchronized concurrent access.** Three threads touch `_simconnect` with no
   lock: the UI thread (`ReceiveMessage`), the data timer (20 Hz), and the
   connection timer. `Close()` can null the handle between another thread's null
   check and its call.
3. **Polling instead of subscription.** A 50 ms timer issues
   `RequestDataOnSimObject(..., PERIOD.ONCE, ...)` — 20 outbound IPC calls per
   second.
4. **Connection setup leak.** If `new SimConnect(...)` succeeds but
   `ConfigureSimconnect()` throws, the half-built handle is retained and the
   reconnect timer never restarts.

Investigating (3) surfaced two further defects:

5. **Orphaned flight on sim exit.** `simconnect_OnRecvQuit` sets `IsConnected =
   false` but never `IsInFlight`. `FlightManager` takes no action on
   `IsConnected`. A flight in progress when MSFS closes is never ended and never
   saved. The guard at `UpdateInFlightState` (`if (IsInFlight && !IsConnected)`)
   is unreachable in practice, because after a disconnect none of the four
   properties that trigger it ever change again.
6. **Dead exception branch.** `simconnect_OnRecvException` tests
   `data.dwException is (uint)0xC000014B`, but `dwException` carries a
   `SIMCONNECT_EXCEPTION` enum value, not an HRESULT. The condition can never be
   true.

## Background

Commit `26be802` (2024-06-12, "Make data calls every 50ms instrad of letting the
sim") replaced a standing `SIMCONNECT_PERIOD.VISUAL_FRAME` subscription with the
current 50 ms poll. The commit message does not record why, and the author does
not recall. It went to `main` without a PR, and the surrounding commits are
unrelated feature work.

The most likely reason is that frame-period delivery stops while the sim is
paused, in a menu, or loading — which would stall `CameraState`, the primary
MSFS 2024 flight start/end signal, because `CameraState` is a field inside the
`FlightData` struct. This design assumes that hypothesis and hedges against it:
camera polling stays on a wall-clock timer, so detection keeps working while the
sim is not running. Note that `SIM_FRAME` shares this limitation with
`VISUAL_FRAME` — neither delivers while the sim is paused — so the split is what
makes the change safe, not the choice of period.

## Design

### 1. Synchronization

A single `private readonly object _simConnectLock = new()` guards every
`_simconnect` access: `ConnectToSimulator`, `ConfigureSimconnect`, all `Request*`
methods, `ReceiveMessage()` in `WndProc`, and `Close()`.

- Remove `[MethodImpl(MethodImplOptions.Synchronized)]` from `Close()`. It locks
  on `this` (publicly reachable) and protects nothing, since no other member
  locks.
- Keep critical sections tight. In `WndProc` the lock wraps `ReceiveMessage()`
  only. The `FlightData` assignment in `Simconnect_OnRecvSimobjectData` stays
  outside any lock: it fires `OnPropertyChanged`, which reaches
  `UpdateInFlightState`, the `FlightManager` state machine, and WPF bindings.
  The state machine calls back into the service (`RequestNearestAirports`,
  `RequestLoadedAircraft`), so holding a lock across that path risks deadlock.
- Widen the catch on guarded calls from `COMException` alone to also cover
  `NullReferenceException` and `ObjectDisposedException`, which become reachable
  when teardown races a request.

Property changes continue to be raised on whatever thread produces them, exactly
as today. Marshalling to the dispatcher is explicitly out of scope.

Expected cost: roughly 40–50 uncontended lock acquisitions per second against
critical sections dominated by cross-process IPC. Negligible.

### 2. Connection loss and recovery

Three failure modes, three responses:

| Trigger | Response |
| --- | --- |
| `OnRecvQuit` (MSFS closed cleanly) | End the flight immediately |
| Pipe error (`0xC00000B0`, `0x800706BA`, unrecognized) | Tear down, reconnect, 60 s grace period |
| `ConfigureSimconnect` throws | Dispose the partial handle, restart reconnect timer |

**`HandleCOMException`.** Every COMException now tears down and reconnects. An
error that leaves the pipe usable is the rare case, so failing closed is safer
than a zombie connection. Individual codes keep distinct log messages.

**`simconnect_OnRecvException`.** Replace the dead HRESULT comparison with
logging of the `SIMCONNECT_EXCEPTION` enum name. These are protocol-level errors
(bad SimVar, unknown request), not pipe failures, and must not trigger teardown.

**`OnRecvQuit`.** Add `IsInFlight = false`, matching what the `0xC000014B` branch
of `HandleCOMException` already does. This fixes the orphaned-flight defect.

**Grace period.** On pipe loss with a flight active: stop the camera timer,
dispose the handle, begin reconnecting, and start a 60 s single-shot grace timer.

- Grace expires with no reconnection → `IsInFlight = false` → `FlightEndedState`
  applies the existing save policy (saves only if `Completed`, or if "save only
  complete flights" is off).
- Reconnection inside the window → cancel the grace timer and run the identity
  check (section 3).

The grace timer is cancelled by *reconnection*, not by a completed identity
check. A reconnect at 55 s whose first data sample arrives at 62 s must not lose
the flight.

**`ConfigureSimconnect` leak.** Wrap configuration so that a failure disposes and
nulls `_simconnect` before restarting the connection timer.

### 3. Flight identity on reconnect

When the pipe recovers within the grace window, decide whether the session is
still the same flight before resuming.

Preconditions and checks, all of which must hold to resume:

1. `IsInFlight` is true. If the sim is no longer in flight, the user quit to the
   menu during the gap — end the flight.
2. Aircraft matches: `title`, and additionally `liveryName` on MSFS 2024,
   following the comparison already used in `FlightManager.SetAircraftAsynchronously`.
3. Position is plausible: within **30 nm airborne** or **20 nm on ground**,
   keyed off `SimOnGround` in the first post-reconnect sample and measured from
   the last known position with `GeoCoordinate.GetDistanceTo`.

The airborne threshold is the looser of the two deliberately: restarting a
flight, getting airborne, and arriving within 30 nm of the last known position
inside 60 s is implausible, whereas a 30 nm ground radius could span a
neighbouring airport.

Pass → resume. Fail → end the flight; normal save policy applies.

**Sequencing.** Reconnection is not instantaneous — `OnRecvOpen` fires, aircraft
data must be re-requested, and the first `FlightData` sample must arrive. The
identity check therefore runs on the first `FlightData` delivery after
reconnection, gated by a `_pendingIdentityCheck` flag, with the flight held in
limbo until then.

**Resuming** rewinds nothing. The state machine is level-triggered: it
re-evaluates from the new sample and continues. `Stopwatch` windows ran through
the gap. The only artifact is a gap in the recorded track; no event is written
for the disconnected period.

**Retained state** — last known latitude/longitude, last `SimOnGround`, and the
active aircraft identity — already lives on `FlightManager` (`ActiveFlight`,
`CurrentFlightParams`), which survives `SimConnectService` teardown.

### 4. Splitting CameraState from FlightData

Three streams replace one:

| Stream | Definition | Period | Approximate rate | Purpose |
| --- | --- | --- | --- | --- |
| `CameraData` | new; `Camera State` only | `ONCE` via 250 ms timer | 4 Hz, unaffected by pause | Flight start/end detection |
| `FlightData` | existing, minus `CameraState` | `SIM_FRAME` subscription | sim rate; 0 while paused | Flight recording |
| `AircraftData` | unchanged | `ONCE`, on demand | — | Aircraft identity |

`CameraState` must be observable exactly when the sim is paused, in a menu, or
loading — precisely when `SIM_FRAME` is silent. Keeping it on a wall-clock poll
preserves current detection behaviour while the bulk data moves to a
subscription.

Net IPC: 20 outbound requests/sec become 4, plus a one-time subscription
registration. Inbound deliveries rise from 20 Hz to sim rate while flying and
fall to zero while paused.

Changes:

- `DataDefinitions` gains `CameraData`; `Requests` gains `CameraDataRequest`.
- New `CameraData` struct with a single `CameraState` field.
- Remove `CameraState` from the `FlightData` struct **and** its
  `AddToDataDefinition` call. Struct field order must match registration order,
  so these are a paired edit.
- `_dataTimer` becomes `_cameraTimer` at 250 ms, calling a new
  `RequestCameraData()`.
- Register the `FlightData` subscription once in `ConfigureSimconnect` with
  `SIMCONNECT_PERIOD.SIM_FRAME` and `SIMCONNECT_DATA_REQUEST_FLAG.CHANGED`;
  delete the per-tick `RequestFlightData()`.
- `Simconnect_OnRecvSimobjectData` gains a `CameraDataRequest` branch that sets
  `CameraState`, and its `FlightData` branch no longer does so.

**`HandleFlightExitEvent` moves to the camera stream.** It is exit detection and
belongs on the stream that survives pause. `FlightManager` drops it from the
`FlightData` case and fires it on `CameraState` delivery instead. This also
closes a hole where pausing after landing and then quitting left a pending
touchdown unfinalized.

### 5. Extracting the flight-state transition table

`UpdateInFlightState` is the least-understood and most defect-prone logic in the
file; it contains the unreachable `!IsConnected` branch, and it is being modified
anyway for the disconnect fix.

Extract the decision into a pure function over
`(cameraState, previousCameraState, pauseState, loadedFlight, isConnected, wasInFlight)`
returning the new in-flight boolean. `SimConnectService` keeps the properties and
side effects; the transition table becomes independently testable across both sim
versions.

## Testing

The solution contains an xUnit test project (`FSTRaK.Tests`, xUnit 2.9.2,
net472/x64) whose existing suites test pure logic over constructed `FlightData`
structs. `TouchdownTrackerTests` is the pattern to follow.

Unit tests (new):

- **Flight identity check** — same aircraft nearby resumes; differing livery ends;
  25 nm airborne resumes; 25 nm on ground ends; exact 20 nm and 30 nm boundaries;
  sim not in flight ends regardless.
- **COMException to recovery action** — `0xC00000B0` maps to reconnect (the
  primary defect); `0xC000014B` maps to reconnect; an unrecognized code maps to
  reconnect.
- **Flight-state transition table** — MSFS 2020 and 2024 start and exit paths;
  VR-toggle and active-pause cases that must not end a flight; the
  disconnect-while-in-flight case that is currently unreachable.

Manual verification on Windows (cannot be automated; requires MSFS):

1. Normal flight start to finish records and saves as before.
2. Quit to main menu mid-flight ends the flight.
3. Pause after landing, then quit — the pending touchdown is finalized.
4. Kill MSFS mid-flight — the flight ends rather than orphaning.
5. Kill and restart MSFS within 60 s, same aircraft and position — the flight
   resumes.
6. Teleport via the world map within 60 s — the flight ends.
7. Confirm the observed `SIM_FRAME` delivery rate and UI responsiveness.

## Risks

- **`SIM_FRAME` delivery rate is unverified.** Estimated at 30–60 Hz from general
  knowledge, not measured; it cannot be measured from the development Mac. A
  substantially higher rate would raise UI-thread binding load proportionally.
  Mitigation if observed: throttle map updates, or return that stream to a timer.
- **This partially reverts `26be802`,** whose motivation is unrecorded. The camera
  split hedges against the most probable cause, but if the 2024 problem was
  something else it may resurface. Manual step 7 is the checkpoint.
- **No build or test execution is possible on the development machine.** All
  verification happens on Windows.

## Out of scope

- Marshalling property-change notifications to the WPF dispatcher.
- Reducing log verbosity in `UpdateInFlightState` (the equality guards on the
  triggering properties already keep it from firing per-tick).
- Any change to save policy, scoring, or the flight event schema.
