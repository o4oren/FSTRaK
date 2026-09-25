# Sim AI Traffic Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Draw the aircraft and helicopters the simulator knows about — AI traffic and real-world live traffic — on the FSTRaK live map, behind a toggle that is off by default and idle when off.

**Architecture:** A 2-second timer inside a new `SimTrafficTracker` drives two one-shot `RequestDataOnSimObjectType` calls (AIRCRAFT, HELICOPTER) at an 80 NM radius. Replies arrive one message per object; the tracker assembles them into per-request batches, excludes the user's own aircraft by object ID, and publishes a merged immutable snapshot. A dedicated `SimTrafficViewModel` turns snapshots into map items for a new `MapItemsControl` layer drawn beneath the VATSIM and IVAO layers.

**Tech Stack:** C# 7.3+/.NET Framework 4.7.2, WPF, `Microsoft.FlightSimulator.SimConnect`, `XAML.MapControl.WPF` 13.4, xUnit 2.9.2, Serilog.

**Spec:** `docs/superpowers/specs/2026-09-25-sim-ai-traffic-design.md`

## Global Constraints

- Target framework `net472`, platform **x64 only**. SimConnect's native DLL makes x86/AnyCPU non-functional.
- **No new NuGet packages.** Everything here uses what the solution already references.
- **No new user settings.** Radius is fixed at 148,160 m (80 NM); poll interval fixed at 2000 ms.
- The traffic toggle starts `false` on every launch and is **not persisted**.
- **Old-style csproj:** `FSTRaK/FSTrAk.csproj` lists every `.cs` file explicitly. Every new source file MUST be added as a `<Compile Include="..." />` entry or it will not build.
- **csproj case alias:** git tracks `FSTRaK/FSTRaK.csproj` as a second path for the same on-disk file. After committing `FSTrAk.csproj`, sync the alias with `git update-index --cacheinfo` (exact command given in each task). Plain `git add FSTRaK/FSTRaK.csproj` stages nothing on a case-insensitive filesystem.
- Nothing in this plan touches the `FlightDataRequest` SIM_FRAME subscription, `NotificationGate`, or the flight state machine. Traffic work must not perturb flight tracking or scoring.
- **Verification constraint:** development happens on macOS, where this solution cannot be built or run. `dotnet test` and MSBuild are unavailable. Every "run the test" step is executed **by the maintainer on Windows**. Do not claim a test passed from the development machine — report code as written-but-unverified.
- Branch: `feat/sim-ai-traffic`. Do not push; the maintainer pushes.

## Deviation from the spec (approved shape, refined detail)

The spec says the detail panel reuses the pilot layout with network-only fields hidden. On reading `ClientDetailPanelControl.xaml`, the pilot panel's route bar, progress bar, and 3×2 stat grid are a single interleaved block — hiding half of it piecemeal would leave a mangled layout. Task 6 instead adds a **separate compact sim-traffic panel**, matching how that file is already organised (pilot panel / airport-ATC panel / CTR-ATC panel are sibling `StackPanel`s). The visible and hidden field lists from the spec are honoured exactly; only the mechanism differs.

## File Structure

**Create:**
- `FSTRaK/BusinessLogic/SimconnectService/SimTrafficTracker.cs` — batch assembly, staleness, user exclusion, poll timer and run-state. Holds no SimConnect handle.
- `FSTRaK/BusinessLogic/SimconnectService/SimTrafficEntry.cs` — DTO pairing a SimConnect object ID with its `SimTrafficData`.
- `FSTRaK/ViewModels/SimTrafficAircraft.cs` — map item model (location, heading, icon, tooltip).
- `FSTRaK/ViewModels/SimTrafficViewModel.cs` — owns the bound list and the toggle.
- `FSTRaK.Tests/SimTrafficTrackerTests.cs`
- `FSTRaK.Tests/SimTrafficAircraftTests.cs`

**Modify:**
- `FSTRaK/DataTypes/SimConnectDataTypes.cs` — `SimTrafficData` struct, two `Requests` members, one `DataDefinitions` member.
- `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs` — definition registration, by-type handler, request issuing, user object ID capture, run-state plumbing, teardown.
- `FSTRaK/ViewModels/LiveViewViewModel.cs` — expose `SimTraffic` and `IsSimConnected`, and one branch in `OnSelectClient`.
- `FSTRaK/ViewModels/SelectedClientViewModel.cs` — sim-traffic constructor and `IsSimTraffic`.
- `FSTRaK/Views/LiveView.xaml` — traffic map layer and toggle button.
- `FSTRaK/Views/ClientDetailPanelControl.xaml` — sim-traffic panel.
- `FSTRaK/Resources/Theme.xaml`, `FSTRaK/Resources/DarkTheme.xaml` — `SimTrafficAircraftColorBrush`.
- `FSTRaK/FSTrAk.csproj` (+ alias) — compile entries for the four new app files.

---

### Task 1: SimConnect data types and the traffic tracker

The whole of the batching, staleness and exclusion logic, test-driven. This is the only task with meaningful unit tests, so it carries them all.

**Files:**
- Modify: `FSTRaK/DataTypes/SimConnectDataTypes.cs`
- Create: `FSTRaK/BusinessLogic/SimconnectService/SimTrafficEntry.cs`
- Create: `FSTRaK/BusinessLogic/SimconnectService/SimTrafficTracker.cs`
- Create: `FSTRaK.Tests/SimTrafficTrackerTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `struct FSTRaK.DataTypes.SimTrafficData` with public fields `Title`, `AtcId`, `AtcType`, `Airline`, `FlightNumber`, `Category` (all `string`), `Latitude`, `Longitude`, `Altitude`, `TrueHeading`, `GroundVelocity` (all `double`), `SimOnGround` (`int`).
  - `Requests.SimTrafficAircraftRequest`, `Requests.SimTrafficHelicopterRequest`, `DataDefinitions.SimTrafficData`.
  - `sealed class FSTRaK.BusinessLogic.SimconnectService.SimTrafficEntry` with `uint ObjectId { get; }` and `SimTrafficData Data { get; }`, constructor `SimTrafficEntry(uint objectId, SimTrafficData data)`.
  - `sealed class FSTRaK.BusinessLogic.SimconnectService.SimTrafficTracker` with:
    - `const double DefaultPollIntervalMs = 2000`
    - `const uint RadiusMeters = 148160`
    - `SimTrafficTracker(Action onPoll)` and `SimTrafficTracker(Action onPoll, double pollIntervalMs)`
    - `event Action<IReadOnlyList<SimTrafficEntry>> TrafficUpdated`
    - `bool IsRunning { get; }`
    - `void SetUserObjectId(uint objectId)`
    - `void UpdateRunState(bool layerEnabled, bool connected, bool simStarted)`
    - `void Poll()`
    - `void Accept(uint requestId, uint objectId, uint entryNumber, uint outOf, SimTrafficData data)`
    - `void Reset()`

- [ ] **Step 1: Add the SimConnect data types**

In `FSTRaK/DataTypes/SimConnectDataTypes.cs`, add the two new members to the existing `Requests` enum (append at the end — the values are only used as request IDs, but appending avoids renumbering anything already in flight):

```csharp
    public enum Requests
    {
        FlightDataRequest,
        NearbyAirportsRequest,
        FlightLoaded,
        AircraftLoaded,
        AircraftDataRequest,
        SimVersionRequest,
        CameraDataRequest,
        SimTrafficAircraftRequest,
        SimTrafficHelicopterRequest
    }
```

Append one member to `DataDefinitions`:

```csharp
    public enum DataDefinitions
    {
        AircraftData,
        FlightData,
        CameraData,
        SimTrafficData
    }
```

And add this struct next to `FlightData` in the same file:

```csharp
    /// <summary>
    /// One traffic object as reported by RequestDataOnSimObjectType. Deliberately small:
    /// this definition is marshalled once per object per poll, and a busy airport can
    /// return hundreds of them.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct SimTrafficData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string AtcId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AtcType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Airline;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FlightNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Category;

        public double Latitude;
        public double Longitude;
        public double Altitude;
        public double TrueHeading;
        public double GroundVelocity;
        public int SimOnGround;
    }
```

- [ ] **Step 2: Add the SimTrafficEntry DTO**

Create `FSTRaK/BusinessLogic/SimconnectService/SimTrafficEntry.cs`:

```csharp
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// One traffic object paired with the SimConnect object ID it arrived under. The ID is
    /// what identifies an object across polls, and what the user-aircraft exclusion keys on.
    /// </summary>
    internal sealed class SimTrafficEntry
    {
        public uint ObjectId { get; }
        public SimTrafficData Data { get; }

        public SimTrafficEntry(uint objectId, SimTrafficData data)
        {
            ObjectId = objectId;
            Data = data;
        }
    }
}
```

- [ ] **Step 3: Write the failing tests**

Create `FSTRaK.Tests/SimTrafficTrackerTests.cs`. Note the long poll interval passed everywhere: the timer must never fire on its own during a test — every cycle is driven by calling `Poll()` explicitly, which keeps these deterministic and fast.

```csharp
using System;
using System.Collections.Generic;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimTrafficTrackerTests
    {
        private const uint AircraftRequest = (uint)Requests.SimTrafficAircraftRequest;
        private const uint HelicopterRequest = (uint)Requests.SimTrafficHelicopterRequest;

        // Long enough that the internal timer never fires during a test; every cycle is
        // driven explicitly by Poll().
        private const double NeverFires = 600000;

        private const uint UserObjectId = 1;

        private static SimTrafficData Data(string atcId)
        {
            return new SimTrafficData
            {
                Title = "Airbus A320",
                AtcId = atcId,
                AtcType = "A320",
                Airline = "",
                FlightNumber = "",
                Category = "Airplane",
                Latitude = 51.0,
                Longitude = -0.5,
                Altitude = 10000,
                TrueHeading = 90,
                GroundVelocity = 320,
                SimOnGround = 0
            };
        }

        private static SimTrafficTracker CreateTracker(out List<IReadOnlyList<SimTrafficEntry>> published)
        {
            var captured = new List<IReadOnlyList<SimTrafficEntry>>();
            var tracker = new SimTrafficTracker(() => { }, NeverFires);
            tracker.TrafficUpdated += snapshot => captured.Add(snapshot);
            tracker.SetUserObjectId(UserObjectId);
            published = captured;
            return tracker;
        }

        [Fact]
        public void Accept_CompleteBatch_PublishesOnceWithEveryEntry()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 2, Data("AAL1"));
            tracker.Accept(AircraftRequest, 11, 2, 2, Data("AAL2"));

            Assert.Single(published);
            Assert.Equal(2, published[0].Count);
        }

        [Fact]
        public void Accept_PartialBatch_PublishesNothing()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 3, Data("AAL1"));
            tracker.Accept(AircraftRequest, 11, 2, 3, Data("AAL2"));

            Assert.Empty(published);
        }

        [Fact]
        public void Accept_UserObjectId_IsExcludedFromTheSnapshot()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, UserObjectId, 1, 2, Data("MINE"));
            tracker.Accept(AircraftRequest, 11, 2, 2, Data("AAL2"));

            Assert.Single(published);
            Assert.Single(published[0]);
            Assert.Equal((uint)11, published[0][0].ObjectId);
        }

        [Fact]
        public void Accept_BeforeUserObjectIdIsKnown_PublishesNothing()
        {
            var published = new List<IReadOnlyList<SimTrafficEntry>>();
            var tracker = new SimTrafficTracker(() => { }, NeverFires);
            tracker.TrafficUpdated += snapshot => published.Add(snapshot);

            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));

            Assert.Empty(published);
        }

        [Fact]
        public void Accept_TornBatch_IsDiscardedRatherThanMerged()
        {
            var tracker = CreateTracker(out var published);

            // A batch that never completes, then a new cycle begins.
            tracker.Accept(AircraftRequest, 10, 1, 5, Data("STALE"));
            tracker.Poll();
            tracker.Accept(AircraftRequest, 20, 1, 1, Data("FRESH"));

            Assert.Single(published);
            Assert.Single(published[0]);
            Assert.Equal((uint)20, published[0][0].ObjectId);
        }

        [Fact]
        public void Accept_AircraftAndHelicopterBatches_MergeIntoOneSnapshot()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            tracker.Accept(HelicopterRequest, 20, 1, 1, Data("HELI1"));

            Assert.Equal(2, published.Count);
            Assert.Equal(2, published[1].Count);
        }

        [Fact]
        public void Poll_TwoSilentCycles_PublishesAnEmptySnapshot()
        {
            var tracker = CreateTracker(out var published);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            published.Clear();

            tracker.Poll();
            Assert.Empty(published);

            tracker.Poll();

            Assert.Single(published);
            Assert.Empty(published[0]);
        }

        [Fact]
        public void Poll_ACompletedBatchResetsTheSilenceCounter()
        {
            var tracker = CreateTracker(out var published);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));

            tracker.Poll();
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            tracker.Poll();
            published.Clear();

            // Only one silent cycle has elapsed since the last completed batch.
            Assert.Empty(published);
        }

        [Fact]
        public void UpdateRunState_RunsOnlyWhenEnabledConnectedAndStarted()
        {
            var tracker = CreateTracker(out _);

            tracker.UpdateRunState(false, true, true);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, false, true);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, true, false);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, true, true);
            Assert.True(tracker.IsRunning);

            tracker.UpdateRunState(false, true, true);
            Assert.False(tracker.IsRunning);
        }

        [Fact]
        public void UpdateRunState_StoppingClearsTheSnapshot()
        {
            var tracker = CreateTracker(out var published);
            tracker.UpdateRunState(true, true, true);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            published.Clear();

            tracker.UpdateRunState(false, true, true);

            Assert.Single(published);
            Assert.Empty(published[0]);
        }

        [Fact]
        public void Poll_InvokesTheRequestCallback()
        {
            var calls = 0;
            var tracker = new SimTrafficTracker(() => calls++, NeverFires);

            tracker.Poll();

            Assert.Equal(1, calls);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run (on Windows): `dotnet test FSTRaK.Tests --filter SimTrafficTrackerTests`
Expected: FAIL — `SimTrafficTracker` and `SimTrafficEntry` do not exist yet (compile error).

- [ ] **Step 5: Implement the tracker**

Create `FSTRaK/BusinessLogic/SimconnectService/SimTrafficTracker.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Keeps the current set of simulator traffic objects.
    ///
    /// RequestDataOnSimObjectType is a one-shot request - there is no PERIOD to attach - so
    /// currency comes from a timer rather than a subscription. Replies arrive one message
    /// per object, each carrying its position in the batch, and a snapshot is published only
    /// when a batch completes: the map must never render half a cycle.
    ///
    /// Holds no SimConnect handle deliberately. Issuing requests is the caller's job, passed
    /// in as onPoll, which is what makes this class testable in isolation - the same shape as
    /// <see cref="NotificationGate"/>.
    ///
    /// Not thread-safe. Like the rest of the SimConnect receive path it is called only from
    /// the WPF UI thread, except for the timer's Elapsed, which is marshalled by the caller.
    /// </summary>
    internal sealed class SimTrafficTracker
    {
        /// <summary>
        /// Two seconds. Fast enough that traffic does not visibly teleport, slow enough that
        /// a few hundred objects per cycle cost nothing measurable.
        /// </summary>
        public const double DefaultPollIntervalMs = 2000;

        /// <summary>80 nautical miles, in metres - SimConnect's radius unit.</summary>
        public const uint RadiusMeters = 148160;

        /// <summary>
        /// Consecutive silent cycles before a request's objects are cleared. A cycle that
        /// finds nothing produces no messages at all, so silence is the only signal that the
        /// traffic is gone; two cycles keeps a single dropped batch from blinking the map.
        /// </summary>
        private const int SilentCyclesBeforeClearing = 2;

        private static readonly uint[] TrackedRequests =
        {
            (uint)Requests.SimTrafficAircraftRequest,
            (uint)Requests.SimTrafficHelicopterRequest
        };

        private readonly Action _onPoll;
        private readonly Timer _pollTimer;

        // Committed objects per request type, replaced wholesale when a batch completes.
        private readonly Dictionary<uint, List<SimTrafficEntry>> _portions =
            new Dictionary<uint, List<SimTrafficEntry>>();

        // Batches still arriving, keyed by request type.
        private readonly Dictionary<uint, List<SimTrafficEntry>> _pending =
            new Dictionary<uint, List<SimTrafficEntry>>();

        private readonly Dictionary<uint, int> _silentCycles = new Dictionary<uint, int>();

        private uint? _userObjectId;

        public event Action<IReadOnlyList<SimTrafficEntry>> TrafficUpdated;

        public bool IsRunning { get; private set; }

        public SimTrafficTracker(Action onPoll)
            : this(onPoll, DefaultPollIntervalMs)
        {
        }

        public SimTrafficTracker(Action onPoll, double pollIntervalMs)
        {
            _onPoll = onPoll;

            foreach (var requestId in TrackedRequests)
            {
                _portions[requestId] = new List<SimTrafficEntry>();
                _silentCycles[requestId] = 0;
            }

            _pollTimer = new Timer(pollIntervalMs) { AutoReset = true };
            _pollTimer.Elapsed += (sender, e) => Poll();
        }

        /// <summary>
        /// The object ID the simulator uses for the user's own aircraft, taken from the
        /// flight data replies. By-type results include the user, and this is how that entry
        /// is recognised - reliably, rather than by assuming an ID or matching on position.
        /// </summary>
        public void SetUserObjectId(uint objectId)
        {
            _userObjectId = objectId;
        }

        /// <summary>
        /// Polling runs only while the layer is on, the connection is up, and the simulator
        /// is running. Switching off stops the timer and clears the map: a toggle that is off
        /// must cost nothing and leave nothing behind.
        /// </summary>
        public void UpdateRunState(bool layerEnabled, bool connected, bool simStarted)
        {
            var shouldRun = layerEnabled && connected && simStarted;

            if (shouldRun == IsRunning)
            {
                return;
            }

            IsRunning = shouldRun;

            if (shouldRun)
            {
                _pollTimer.Start();
            }
            else
            {
                _pollTimer.Stop();
                Reset();
            }
        }

        /// <summary>
        /// Opens a cycle: ages the silence counters, discards any batch left incomplete by
        /// the previous cycle, then asks the caller to issue the requests.
        /// </summary>
        public void Poll()
        {
            var changed = false;

            foreach (var requestId in TrackedRequests)
            {
                // A batch still pending when the next cycle opens lost messages somewhere.
                // Discard it - a snapshot must never mix two cycles.
                _pending.Remove(requestId);

                _silentCycles[requestId] = _silentCycles[requestId] + 1;

                if (_silentCycles[requestId] >= SilentCyclesBeforeClearing && _portions[requestId].Count > 0)
                {
                    _portions[requestId] = new List<SimTrafficEntry>();
                    changed = true;
                }
            }

            if (changed)
            {
                Publish();
            }

            _onPoll?.Invoke();
        }

        /// <summary>
        /// Folds one by-type reply into the batch it belongs to, publishing when the batch
        /// completes. Entry numbers are 1-based.
        /// </summary>
        public void Accept(uint requestId, uint objectId, uint entryNumber, uint outOf, SimTrafficData data)
        {
            if (!_portions.ContainsKey(requestId) || outOf == 0)
            {
                return;
            }

            // Until the user's own object ID is known, any snapshot risks drawing a duplicate
            // of the user's aircraft. Publishing nothing is the safer failure.
            if (_userObjectId == null)
            {
                return;
            }

            if (entryNumber == 1)
            {
                _pending[requestId] = new List<SimTrafficEntry>();
            }

            if (!_pending.TryGetValue(requestId, out var batch))
            {
                // Mid-batch arrival with no start - the head of this batch was lost.
                return;
            }

            if (objectId != _userObjectId.Value)
            {
                batch.Add(new SimTrafficEntry(objectId, data));
            }

            if (entryNumber >= outOf)
            {
                _portions[requestId] = batch;
                _pending.Remove(requestId);
                _silentCycles[requestId] = 0;
                Publish();
            }
        }

        /// <summary>
        /// Drops everything and publishes an empty snapshot. Used when the layer is switched
        /// off and when the connection dies, so no traffic is left frozen on the map.
        /// </summary>
        public void Reset()
        {
            _pending.Clear();

            foreach (var requestId in TrackedRequests)
            {
                _portions[requestId] = new List<SimTrafficEntry>();
                _silentCycles[requestId] = 0;
            }

            Publish();
        }

        private void Publish()
        {
            var merged = TrackedRequests
                .SelectMany(requestId => _portions[requestId])
                .ToList();

            TrafficUpdated?.Invoke(merged);
        }
    }
}
```

- [ ] **Step 6: Register the new files in the csproj**

In `FSTRaK/FSTrAk.csproj`, next to the existing `<Compile Include="BusinessLogic\SimconnectService\NotificationGate.cs" />` line, add:

```xml
    <Compile Include="BusinessLogic\SimconnectService\SimTrafficEntry.cs" />
    <Compile Include="BusinessLogic\SimconnectService\SimTrafficTracker.cs" />
```

`FSTRaK.Tests.csproj` is SDK-style and globs its sources — the new test file needs no entry.

- [ ] **Step 7: Run the tests to verify they pass**

Run (on Windows): `dotnet test FSTRaK.Tests --filter SimTrafficTrackerTests`
Expected: PASS, 11 tests.

- [ ] **Step 8: Commit**

```bash
git add FSTRaK/DataTypes/SimConnectDataTypes.cs \
        FSTRaK/BusinessLogic/SimconnectService/SimTrafficEntry.cs \
        FSTRaK/BusinessLogic/SimconnectService/SimTrafficTracker.cs \
        FSTRaK.Tests/SimTrafficTrackerTests.cs \
        FSTRaK/FSTrAk.csproj
git commit -m "feat: track simulator traffic objects

Batch assembly, per-request staleness and user-aircraft exclusion for
by-type traffic replies. Holds no SimConnect handle, so the whole of the
logic is unit tested."
git update-index --cacheinfo 100644,$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj),FSTRaK/FSTRaK.csproj
git commit -m "chore: sync the FSTRaK.csproj case alias"
```

---

### Task 2: Wire the tracker into SimConnectService

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs`

**Interfaces:**
- Consumes: `SimTrafficTracker`, `SimTrafficEntry`, `SimTrafficData`, `Requests.SimTrafficAircraftRequest`, `Requests.SimTrafficHelicopterRequest`, `DataDefinitions.SimTrafficData` (Task 1).
- Produces, on `SimConnectService`:
  - `event Action<IReadOnlyList<SimTrafficEntry>> SimTrafficUpdated`
  - `void SetSimTrafficEnabled(bool enabled)`

No unit tests: every line here needs a live SimConnect handle. Behaviour is verified in the manual pass at the end of Task 6.

- [ ] **Step 1: Add the field and the traffic state**

In `SimConnectService.cs`, after the `_flightDataNotificationGate` field (around line 66), add:

```csharp
    /// <summary>
    /// Traffic polling is independent of the flight data subscription by design: a slow or
    /// failed traffic request must never perturb the SIM_FRAME path that drives the state
    /// machine.
    /// </summary>
    private SimTrafficTracker _simTrafficTracker;

    private bool _simTrafficEnabled;
```

- [ ] **Step 2: Create the tracker in Initialize**

In `Initialize()`, before `SetCameraTimer();`, add:

```csharp
        SetSimTrafficTracker();
```

And add this method next to `SetCameraTimer`:

```csharp
    private void SetSimTrafficTracker()
    {
        _simTrafficTracker = new SimTrafficTracker(RequestSimTraffic);
        _simTrafficTracker.TrafficUpdated += snapshot => SimTrafficUpdated?.Invoke(snapshot);
    }
```

- [ ] **Step 3: Add the public surface**

Next to the `PropertyChanged` event declaration (around line 286), add:

```csharp
    /// <summary>
    /// Raised with the current set of traffic objects each time a poll cycle completes, and
    /// with an empty list when traffic is switched off or the connection drops.
    /// </summary>
    public event Action<IReadOnlyList<SimTrafficEntry>> SimTrafficUpdated;
```

And next to `RequestCameraData` (around line 970), add:

```csharp
    /// <summary>
    /// Turns the traffic layer on or off. Polling additionally requires a live connection
    /// and a running simulator, so this only expresses the user's intent.
    /// </summary>
    public void SetSimTrafficEnabled(bool enabled)
    {
        _simTrafficEnabled = enabled;
        UpdateSimTrafficRunState();
    }

    private void UpdateSimTrafficRunState()
    {
        _simTrafficTracker?.UpdateRunState(_simTrafficEnabled, IsConnected, SimStarted);
    }

    /// <summary>
    /// Two one-shot by-type requests per cycle. RequestDataOnSimObjectType has no PERIOD, so
    /// currency comes from the tracker's timer. Both go through SafeSimConnectCall: a failed
    /// traffic request degrades the layer, never the connection.
    /// </summary>
    private void RequestSimTraffic()
    {
        SafeSimConnectCall(sc =>
        {
            sc.RequestDataOnSimObjectType(Requests.SimTrafficAircraftRequest, DataDefinitions.SimTrafficData,
                SimTrafficTracker.RadiusMeters, SIMCONNECT_SIMOBJECT_TYPE.AIRCRAFT);
            sc.RequestDataOnSimObjectType(Requests.SimTrafficHelicopterRequest, DataDefinitions.SimTrafficData,
                SimTrafficTracker.RadiusMeters, SIMCONNECT_SIMOBJECT_TYPE.HELICOPTER);
        }, nameof(RequestSimTraffic));
    }
```

- [ ] **Step 4: Drive the run state from connection and sim state**

`UpdateSimTrafficRunState` must be called whenever `IsConnected` or `SimStarted` changes. In the `IsConnected` setter, after `OnPropertyChanged();`, add:

```csharp
                UpdateSimTrafficRunState();
```

Do the same in the `SimStarted` setter, after its `OnPropertyChanged();`.

- [ ] **Step 5: Register the data definition**

In `ConfigureSimconnect()`, after the `CameraData` definition block (around line 645) and before the `RegisterDataDefineStruct` calls, add:

```csharp
        // TRAFFIC - one definition reused for both the aircraft and helicopter by-type
        // requests. Field order must match SimTrafficData exactly.
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Title", null,
            SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "ATC ID", null,
            SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "ATC Type", null,
            SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "ATC Airline", null,
            SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "ATC Flight Number", null,
            SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Category", null,
            SIMCONNECT_DATATYPE.STRING128, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Plane Latitude", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Plane Longitude", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Plane Altitude", "feet",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Plane Heading Degrees True", "degrees",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Ground Velocity", "knots",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.AddToDataDefinition(DataDefinitions.SimTrafficData, "Sim On Ground", "Bool",
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
```

Then add the struct registration next to the existing three:

```csharp
        _simconnect.RegisterDataDefineStruct<SimTrafficData>(DataDefinitions.SimTrafficData);
```

And subscribe the by-type handler next to the other `OnRecv` registrations (around line 662):

```csharp
        _simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(Simconnect_OnRecvSimobjectDataBytype);
```

- [ ] **Step 6: Handle the by-type replies**

Add this method directly after `Simconnect_OnRecvSimobjectData` (after line 864):

```csharp
    /// <summary>
    /// By-type replies arrive one message per object. The tracker assembles them; this only
    /// unpacks and forwards.
    /// </summary>
    private void Simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        try
        {
            if (data.dwRequestID != (uint)Requests.SimTrafficAircraftRequest &&
                data.dwRequestID != (uint)Requests.SimTrafficHelicopterRequest)
            {
                return;
            }

            _simTrafficTracker?.Accept(
                data.dwRequestID,
                data.dwObjectID,
                data.dwentrynumber,
                data.dwoutof,
                (SimTrafficData)data.dwData[0]);
        }
        catch (COMException ex)
        {
            HandleCOMException(ex);
        }
    }
```

- [ ] **Step 7: Capture the user's object ID**

In `Simconnect_OnRecvSimobjectData`, inside the `FlightDataRequest` branch, immediately after `FlightData = (FlightData)data.dwData[0];` add:

```csharp
                // The simulator's own object ID for the user aircraft. By-type traffic
                // results include the user, and this is what the tracker excludes.
                _simTrafficTracker?.SetUserObjectId(data.dwObjectID);
```

- [ ] **Step 8: Clear traffic when the connection goes away**

In `HandleConnectionLost()`, after the `StopGettingData();` call, add:

```csharp
        _simTrafficTracker?.UpdateRunState(false, false, false);
```

Add the identical line in `simconnect_OnRecvQuit()`, also after its `StopGettingData();`.

Note: this leaves `_simTrafficEnabled` untouched on purpose — the user's choice survives a reconnect, and `UpdateSimTrafficRunState` restarts polling from the `IsConnected` setter when the sim comes back.

- [ ] **Step 9: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs
git commit -m "feat: poll the simulator for traffic objects

Two by-type requests per cycle behind SafeSimConnectCall, with the
user's own object ID captured from the flight data replies so the
tracker can exclude it. Polling stops and clears on disconnect."
```

---

### Task 3: The map item model

**Files:**
- Create: `FSTRaK/ViewModels/SimTrafficAircraft.cs`
- Create: `FSTRaK.Tests/SimTrafficAircraftTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: `SimTrafficEntry`, `SimTrafficData` (Task 1).
- Produces: `internal sealed class FSTRaK.ViewModels.SimTrafficAircraft` with constructor `SimTrafficAircraft(SimTrafficEntry entry)` and properties `uint ObjectId`, `string Callsign`, `string AircraftType`, `string Airline`, `MapControl.Location Location`, `double Heading`, `int Altitude`, `int Groundspeed`, `bool IsOnGround`, `string IconResource`, `double ScaleFactor`, `string TooltipText`.

- [ ] **Step 1: Write the failing tests**

Create `FSTRaK.Tests/SimTrafficAircraftTests.cs`:

```csharp
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using FSTRaK.ViewModels;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimTrafficAircraftTests
    {
        private static SimTrafficAircraft Build(string atcId, string flightNumber, string title)
        {
            var data = new SimTrafficData
            {
                Title = title,
                AtcId = atcId,
                AtcType = "B738",
                Airline = "Ryanair",
                FlightNumber = flightNumber,
                Category = "Airplane",
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 12345.6,
                TrueHeading = 271.4,
                GroundVelocity = 289.7,
                SimOnGround = 0
            };

            return new SimTrafficAircraft(new SimTrafficEntry(42, data));
        }

        [Fact]
        public void Callsign_PrefersAtcId()
        {
            Assert.Equal("EIDYH", Build("EIDYH", "RYR123", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Callsign_FallsBackToFlightNumberWhenAtcIdIsBlank()
        {
            Assert.Equal("RYR123", Build("   ", "RYR123", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Callsign_FallsBackToTitleWhenAtcIdAndFlightNumberAreBlank()
        {
            Assert.Equal("Boeing 737-800", Build("", "", "Boeing 737-800").Callsign);
        }

        [Fact]
        public void Location_UsesTheReportedCoordinates()
        {
            var aircraft = Build("EIDYH", "", "Boeing 737-800");

            Assert.Equal(51.5, aircraft.Location.Latitude);
            Assert.Equal(-0.45, aircraft.Location.Longitude);
        }

        [Fact]
        public void AltitudeAndGroundspeed_AreRoundedToWholeUnits()
        {
            var aircraft = Build("EIDYH", "", "Boeing 737-800");

            Assert.Equal(12346, aircraft.Altitude);
            Assert.Equal(290, aircraft.Groundspeed);
        }

        [Fact]
        public void IconResource_IsResolvedFromTheAtcType()
        {
            Assert.False(string.IsNullOrEmpty(Build("EIDYH", "", "Boeing 737-800").IconResource));
        }

        [Fact]
        public void TooltipText_IncludesTheCallsignAndType()
        {
            var tooltip = Build("EIDYH", "", "Boeing 737-800").TooltipText;

            Assert.Contains("EIDYH", tooltip);
            Assert.Contains("B738", tooltip);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (on Windows): `dotnet test FSTRaK.Tests --filter SimTrafficAircraftTests`
Expected: FAIL — `SimTrafficAircraft` does not exist (compile error).

- [ ] **Step 3: Implement the model**

Create `FSTRaK/ViewModels/SimTrafficAircraft.cs`:

```csharp
using System;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;
using MapControl;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// One simulator traffic object as the map draws it. Built fresh from each snapshot
    /// rather than mutated, which is why it carries no change notification.
    /// </summary>
    internal sealed class SimTrafficAircraft
    {
        public uint ObjectId { get; }
        public string Callsign { get; }
        public string AircraftType { get; }
        public string Airline { get; }
        public Location Location { get; }
        public double Heading { get; }
        public int Altitude { get; }
        public int Groundspeed { get; }
        public bool IsOnGround { get; }
        public string IconResource { get; }
        public double ScaleFactor { get; }

        public SimTrafficAircraft(SimTrafficEntry entry)
        {
            var data = entry.Data;

            ObjectId = entry.ObjectId;
            AircraftType = (data.AtcType ?? string.Empty).Trim();
            Airline = (data.Airline ?? string.Empty).Trim();
            Callsign = ResolveCallsign(data.AtcId, data.FlightNumber, data.Title);
            Location = new Location(data.Latitude, data.Longitude);
            Heading = data.TrueHeading;
            Altitude = (int)Math.Round(data.Altitude);
            Groundspeed = (int)Math.Round(data.GroundVelocity);
            IsOnGround = data.SimOnGround != 0;

            var (icon, scale) = AircraftResolver.GetAircraftIcon(AircraftType);
            IconResource = icon;
            ScaleFactor = scale;
        }

        /// <summary>
        /// AI objects are inconsistently labelled: sim traffic usually carries an ATC ID,
        /// live traffic often only a flight number, and multiplayer aircraft sometimes
        /// neither. The title is the last resort so the map never shows a blank label.
        /// </summary>
        private static string ResolveCallsign(string atcId, string flightNumber, string title)
        {
            var id = (atcId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            var number = (flightNumber ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(number))
            {
                return number;
            }

            return (title ?? string.Empty).Trim();
        }

        public string TooltipText => CreateTooltipText();

        private string CreateTooltipText()
        {
            var airline = string.IsNullOrEmpty(Airline) ? "" : $"{Airline}\n";
            var state = IsOnGround ? "ON GROUND" : $"ALT: {Altitude:N0}";
            return $"{Callsign}\n{airline}{AircraftType}\n{state}  GS: {Groundspeed}  HDG: {Heading:F0}";
        }
    }
}
```

- [ ] **Step 4: Register the file in the csproj**

In `FSTRaK/FSTrAk.csproj`, next to `<Compile Include="ViewModels\SelectedClientViewModel.cs" />`, add:

```xml
    <Compile Include="ViewModels\SimTrafficAircraft.cs" />
```

- [ ] **Step 5: Run the tests to verify they pass**

Run (on Windows): `dotnet test FSTRaK.Tests --filter SimTrafficAircraftTests`
Expected: PASS, 7 tests.

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/ViewModels/SimTrafficAircraft.cs \
        FSTRaK.Tests/SimTrafficAircraftTests.cs \
        FSTRaK/FSTrAk.csproj
git commit -m "feat: add the sim traffic map item model

Callsign falls back ATC ID to flight number to title, because AI objects
are inconsistently labelled and a blank map label helps nobody."
git update-index --cacheinfo 100644,$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj),FSTRaK/FSTRaK.csproj
git commit -m "chore: sync the FSTRaK.csproj case alias"
```

---

### Task 4: The traffic layer view model

**Files:**
- Create: `FSTRaK/ViewModels/SimTrafficViewModel.cs`
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: `SimConnectService.SimTrafficUpdated`, `SimConnectService.SetSimTrafficEnabled` (Task 2); `SimTrafficAircraft` (Task 3).
- Produces:
  - `internal sealed class FSTRaK.ViewModels.SimTrafficViewModel : BaseViewModel` with `BindingList<SimTrafficAircraft> Aircraft { get; }` and `bool IsShowSimTraffic { get; set; }`.
  - On `LiveViewViewModel`: `SimTrafficViewModel SimTraffic { get; }` and `bool IsSimConnected { get; }`.

No unit tests: this is a thin binding shim over a tested tracker, and `BindingList` change notification is only meaningful under a live WPF dispatcher.

- [ ] **Step 1: Create the view model**

Create `FSTRaK/ViewModels/SimTrafficViewModel.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.Utils;

namespace FSTRaK.ViewModels
{
    /// <summary>
    /// The simulator traffic layer. Kept apart from LiveViewViewModel deliberately: that
    /// file already carries the VATSIM and IVAO layers, and this is a third source with its
    /// own lifecycle.
    /// </summary>
    internal sealed class SimTrafficViewModel : BaseViewModel
    {
        private readonly SimConnectService _simConnectService = SimConnectService.Instance;

        public BindingList<SimTrafficAircraft> Aircraft { get; } = new BindingList<SimTrafficAircraft>();

        private bool _isShowSimTraffic;

        /// <summary>
        /// Off on every launch, and deliberately not persisted. Switching it off stops the
        /// polling outright rather than merely hiding the layer.
        /// </summary>
        public bool IsShowSimTraffic
        {
            get => _isShowSimTraffic;
            set
            {
                if (value == _isShowSimTraffic)
                {
                    return;
                }

                _isShowSimTraffic = value;
                OnPropertyChanged();
                _simConnectService.SetSimTrafficEnabled(value);
            }
        }

        public SimTrafficViewModel()
        {
            _simConnectService.SimTrafficUpdated += OnTrafficUpdated;
        }

        /// <summary>
        /// Snapshots arrive on the SimConnect receive path, which is the UI thread - but the
        /// tracker's poll timer can also publish an empty snapshot from a timer thread when a
        /// cycle goes silent, so the marshalling below is not optional.
        /// </summary>
        private void OnTrafficUpdated(IReadOnlyList<SimTrafficEntry> snapshot)
        {
            var items = snapshot.Select(entry => new SimTrafficAircraft(entry)).ToList();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Aircraft.ReplaceContent(items);
                return;
            }

            dispatcher.BeginInvoke(new System.Action(() => Aircraft.ReplaceContent(items)));
        }
    }
}
```

- [ ] **Step 2: Expose it from LiveViewViewModel**

In `FSTRaK/ViewModels/LiveViewViewModel.cs`, add a property next to the other service fields near the top of the class (after the `_simBriefService` field):

```csharp
        /// <summary>
        /// The simulator traffic layer. Independent of the VATSIM/IVAO network selector -
        /// both can be shown at once.
        /// </summary>
        public SimTrafficViewModel SimTraffic { get; } = new SimTrafficViewModel();

        private bool _isSimConnected;

        /// <summary>
        /// Drives the visibility of the traffic toggle: traffic only exists while attached to
        /// a running simulator.
        /// </summary>
        public bool IsSimConnected
        {
            get => _isSimConnected;
            private set { _isSimConnected = value; OnPropertyChanged(); }
        }
```

- [ ] **Step 3: Keep IsSimConnected current**

In the property-changed switch (around line 1596), the existing case reads:

```csharp
                case nameof(_flightManager.SimConnectIsConnected):
                    ConnectionText = $"{(_flightManager.SimConnectIsConnected ? "Connected to " : "Not connected to sim")} {(_flightManager.SimVersion != null ? _flightManager.SimVersion : "")}";
```

Add one line immediately after that assignment, before the case's `break`:

```csharp
                    IsSimConnected = _flightManager.SimConnectIsConnected;
```

- [ ] **Step 4: Register the file in the csproj**

In `FSTRaK/FSTrAk.csproj`, next to the entry added in Task 3, add:

```xml
    <Compile Include="ViewModels\SimTrafficViewModel.cs" />
```

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/ViewModels/SimTrafficViewModel.cs \
        FSTRaK/ViewModels/LiveViewViewModel.cs \
        FSTRaK/FSTrAk.csproj
git commit -m "feat: add the sim traffic layer view model

Kept out of LiveViewViewModel, which already carries two traffic
sources; it exposes the layer as a single property."
git update-index --cacheinfo 100644,$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj),FSTRaK/FSTRaK.csproj
git commit -m "chore: sync the FSTRaK.csproj case alias"
```

---

### Task 5: Map layer, toggle and theme brushes

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml`
- Modify: `FSTRaK/Resources/Theme.xaml`
- Modify: `FSTRaK/Resources/DarkTheme.xaml`

**Interfaces:**
- Consumes: `LiveViewViewModel.SimTraffic`, `LiveViewViewModel.IsSimConnected` (Task 4); `SimTrafficAircraft.Location/Heading/IconResource/ScaleFactor/TooltipText` (Task 3).
- Produces: the `SimTrafficAircraftColorBrush` resource key, used by the layer template.

- [ ] **Step 1: Add the theme brushes**

In `FSTRaK/Resources/Theme.xaml`, next to line 125 (`IvaoAircraftColorBrush`), add:

```xml
    <SolidColorBrush x:Key="SimTrafficAircraftColorBrush" Color="#0F9D6E"/>
```

In `FSTRaK/Resources/DarkTheme.xaml`, next to line 131, add:

```xml
    <SolidColorBrush x:Key="SimTrafficAircraftColorBrush" Color="#34D399"/>
```

Green reads clearly against both map themes and is unmistakable next to the VATSIM blue and the IVAO orange.

- [ ] **Step 2: Add the map layer**

In `FSTRaK/Views/LiveView.xaml`, insert this block **immediately before** the `<!-- IVAO Aircraft -->` comment at line 305. Position matters: sim traffic draws beneath both network layers, which keeps the existing "Vatsim Aircraft (top network layer)" ordering intact.

```xml
            <!-- Simulator traffic (below the network layers) -->
            <map:MapItemsControl ItemsSource="{Binding SimTraffic.Aircraft}"
                                 Visibility="{Binding SimTraffic.IsShowSimTraffic, Converter={StaticResource BoolToVis}}">
                <map:MapItemsControl.ItemContainerStyle>
                    <Style TargetType="map:MapItem">
                        <Setter Property="Location" Value="{Binding Location}"/>
                        <Setter Property="Margin" Value="-12 -12 0 0"/>
                        <EventSetter Event="MouseLeftButtonUp" Handler="OnMapItemClicked"/>
                    </Style>
                </map:MapItemsControl.ItemContainerStyle>
                <map:MapItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Path Fill="{DynamicResource SimTrafficAircraftColorBrush}"
                              Data="{Binding IconResource, Converter={StaticResource ResourceNameToGeometryConverter}}">
                            <Path.RenderTransform>
                                <TransformGroup>
                                    <ScaleTransform CenterX="16" CenterY="16"
                                                    ScaleX="{Binding ScaleFactor}" ScaleY="{Binding ScaleFactor}"/>
                                    <RotateTransform CenterX="16" CenterY="16" Angle="{Binding Heading}"/>
                                </TransformGroup>
                            </Path.RenderTransform>
                            <Path.ToolTip>
                                <ToolTip Content="{Binding TooltipText}"/>
                            </Path.ToolTip>
                        </Path>
                    </DataTemplate>
                </map:MapItemsControl.ItemTemplate>
            </map:MapItemsControl>

```

- [ ] **Step 3: Add the toggle button**

In the right-hand `StackPanel`, insert this **after** the ATC toggle's closing `</ToggleButton>` (line 475) and before the SimBrief plan toggle. Unlike the Pilots and ATC toggles, it keys off the sim connection rather than `IsAnyNetworkActive`, because it has nothing to do with the network selector:

```xml
            <!-- Simulator traffic toggle - independent of the network selector -->
            <ToggleButton Style="{DynamicResource MapToggleButton}"
                          IsChecked="{Binding SimTraffic.IsShowSimTraffic}"
                          Visibility="{Binding IsSimConnected, Converter={StaticResource BoolToVis}}"
                          ToolTip="Show aircraft and helicopters in the simulator">
                <TextBlock Foreground="{DynamicResource SuperBrightTextColor}">Traffic</TextBlock>
            </ToggleButton>
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/Views/LiveView.xaml \
        FSTRaK/Resources/Theme.xaml \
        FSTRaK/Resources/DarkTheme.xaml
git commit -m "feat: draw simulator traffic on the live map

The layer sits below the network layers so VATSIM and IVAO clients stay
on top, and its toggle is independent of the network selector."
```

---

### Task 6: Detail panel

Completes the feature: clicking a traffic aircraft opens the panel.

**Files:**
- Modify: `FSTRaK/ViewModels/SelectedClientViewModel.cs`
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`
- Modify: `FSTRaK/Views/ClientDetailPanelControl.xaml`

**Interfaces:**
- Consumes: `SimTrafficAircraft` (Task 3).
- Produces: `SelectedClientViewModel(SimTrafficAircraft item)` and `bool IsSimTraffic { get; }`.

- [ ] **Step 1: Add the constructor and the flag**

In `FSTRaK/ViewModels/SelectedClientViewModel.cs`, add to the network identity helpers (after `IsIvao` on line 50):

```csharp
        public bool IsSimTraffic { get; private set; }

        /// <summary>
        /// The pilot panel is for network clients. Sim traffic reuses ClientType.Pilot for
        /// its identity but renders through its own, much smaller panel - SimConnect exposes
        /// no flight plan, route, or progress for AI objects.
        /// </summary>
        public bool IsNetworkPilot => IsPilot && !IsSimTraffic;

        public string OnGroundDisplay { get; private set; }
```

Add the raw reference next to the others (after line 89):

```csharp
        public SimTrafficAircraft SimTrafficItem { get; private set; }
```

And add this constructor after the IVAO pilot constructor (after line 141):

```csharp
        // ── Simulator traffic constructor
        public SelectedClientViewModel(SimTrafficAircraft item)
        {
            Network = NetworkType.None;
            ClientKind = ClientType.Pilot;
            IsSimTraffic = true;
            SimTrafficItem = item;
            IsOwnAircraft = false;
            IsOwnAircraftInFlight = false;
            Callsign = item.Callsign;
            PilotName = "";
            AircraftType = item.AircraftType;
            Altitude = item.Altitude;
            Groundspeed = item.Groundspeed;
            Heading = (int)Math.Round(item.Heading);
            OnGroundDisplay = item.IsOnGround ? "ON GROUND" : "AIRBORNE";
            Departure = "";
            Arrival = "";
            FlightRules = "";
            Squawk = "";
            CruiseAlt = "";
            RouteString = "";
            Remarks = "";
            OnlineTime = "";
        }
```

`Airline` is not an existing property on this class, so add it beside `AircraftType` (line 59):

```csharp
        public string Airline { get; private set; }
```

and set it in the new constructor, after `AircraftType`:

```csharp
            Airline = item.Airline;
```

- [ ] **Step 2: Route the click**

In `FSTRaK/ViewModels/LiveViewViewModel.cs`, inside `OnSelectClient`, add this branch after the `IvaoAircraft` branch (after line 709):

```csharp
            else if (parameter is SimTrafficAircraft sta)
            {
                // No track history and no airport coordinates: SimConnect exposes neither for
                // AI objects, so the panel opens with what the snapshot carries and nothing
                // is fetched.
                SelectedClient = new SelectedClientViewModel(sta);
            }
```

- [ ] **Step 3: Gate the existing pilot panel**

In `FSTRaK/Views/ClientDetailPanelControl.xaml`, change line 107 from:

```xml
            <StackPanel Visibility="{Binding IsPilot, Converter={StaticResource BoolToVis}}">
```

to:

```xml
            <StackPanel Visibility="{Binding IsNetworkPilot, Converter={StaticResource BoolToVis}}">
```

- [ ] **Step 4: Add the sim traffic panel**

Insert this immediately after the pilot panel's closing `</StackPanel>` (line 223), before the `<!-- ══════════════ AIRPORT ATC PANEL ══════════════ -->` comment:

```xml
            <!-- ══════════════ SIM TRAFFIC PANEL ══════════════ -->
            <StackPanel Visibility="{Binding IsSimTraffic, Converter={StaticResource BoolToVis}}">

                <StackPanel Margin="0,0,0,8">
                    <TextBlock Text="{Binding Airline}" Style="{StaticResource DimText}"
                               Visibility="{Binding Airline, Converter={StaticResource NullToVis}}"/>
                    <TextBlock Text="{Binding AircraftType}" Style="{StaticResource DimText}"/>
                    <TextBlock Text="Simulator traffic" Style="{StaticResource DimText}" Opacity="0.5"/>
                </StackPanel>

                <UniformGrid Columns="3" Margin="0,0,0,6">
                    <Border Style="{StaticResource StatTile}" Margin="0,0,2,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Altitude, StringFormat={}{0:N0}}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="ALT ft" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="1,0,1,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Groundspeed}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="GS kts" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="2,0,0,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Heading, StringFormat={}{0}°}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="HDG" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                </UniformGrid>

                <Border Style="{StaticResource StatTile}">
                    <StackPanel>
                        <TextBlock Text="{Binding OnGroundDisplay}" Style="{StaticResource StatValue}"/>
                        <TextBlock Text="STATE" Style="{StaticResource StatLabel}"/>
                    </StackPanel>
                </Border>
            </StackPanel>

```

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/ViewModels/SelectedClientViewModel.cs \
        FSTRaK/ViewModels/LiveViewViewModel.cs \
        FSTRaK/Views/ClientDetailPanelControl.xaml
git commit -m "feat: show a detail panel for simulator traffic

A separate compact panel rather than the network pilot layout: there is
no flight plan, route or progress to show for an AI object."
```

- [ ] **Step 6: Full manual verification on Windows**

Build `FSTRaK.sln` in `Release|x64` (or `Debug|x64`) and run `dotnet test FSTRaK.Tests` — 18 new tests plus the existing suite. Then, with MSFS running and a flight loaded at a busy airport with AI traffic enabled in the sim:

1. Traffic toggle is **absent** before FSTRaK connects, and appears once connected.
2. Toggle starts **off**. With it off, the log shows no `RequestSimTraffic` activity and no traffic is drawn.
3. Switch on: aircraft appear within a couple of seconds, pointing along their headings, and step position roughly every 2 s.
4. Your own aircraft is **not** drawn as traffic — only one aircraft symbol at your position.
5. Hover a traffic aircraft: tooltip shows callsign, type, altitude/on-ground, GS, heading.
6. Click one: the detail panel opens with the sim traffic layout, no empty route/progress/ATIS blocks.
7. Switch the VATSIM layer on as well: both layers draw, VATSIM on top.
8. Switch traffic off: every traffic symbol disappears immediately.
9. Fly away from traffic until none is in range: symbols clear rather than freezing in place.
10. Close MSFS while traffic is on: symbols clear, no errors in the log, FSTRaK reconnects normally when MSFS restarts and traffic resumes.
11. Fly a short circuit and land: the flight is logged with a landing score, unchanged by any of the above.

Record the result. If anything fails, that is a finding to fix before merge — not something to paper over.

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| AIRCRAFT + HELICOPTER object types | 2 (requests), 1 (merge) |
| 2 s poll, 80 NM fixed radius, no settings | 1 (constants), 2 (requests) |
| Snapshot polling, not per-object subscriptions | 2 |
| Batch assembly, publish only on completion | 1 |
| Torn batch discarded, never merged | 1 |
| Two silent cycles ⇒ empty snapshot | 1 |
| User aircraft excluded by observed `dwObjectID` | 1 (exclusion), 2 (capture) |
| Nothing published before the user ID is known | 1 |
| Tracker holds no handle, unit tested | 1 |
| `SafeSimConnectCall` for every request | 2 |
| Timer stops and clears on disconnect | 2 |
| Toggle off by default, not persisted | 4 |
| Independent of the network selector | 5 |
| Layer below VATSIM/IVAO | 5 |
| Distinct brush, both themes | 5 |
| Icon + rotation + tooltip | 3, 5 |
| Clickable detail panel, network-only fields absent | 6 |
| Callsign fallback ATC ID → flight number → title | 3 |
| `NetworkType` unchanged | 6 |
| No new NuGet packages, no new settings | Global Constraints |

No gaps.

**Placeholder scan:** none — every code step carries the literal code to write, and every test step names the command and the expected result.

**Type consistency:** `SimTrafficEntry(uint, SimTrafficData)` is constructed in Task 1 and consumed in Tasks 3 and 4 with the same shape. `ScaleFactor` is spelled consistently in Task 3 and the Task 5 binding (note: **not** the misspelled `ScaleFactror` the VATSIM layer uses — that typo is not propagated). `SimTrafficUpdated` carries `IReadOnlyList<SimTrafficEntry>` in Tasks 2 and 4. `Requests` members are cast to `uint` on both the publish and accept sides.

One thing Task 6 adds that the spec did not enumerate: `Airline` and `OnGroundDisplay` on `SelectedClientViewModel`, needed by the panel the spec's visible-field list calls for.
