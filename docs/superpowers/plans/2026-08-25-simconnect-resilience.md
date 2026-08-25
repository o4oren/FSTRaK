# SimConnect Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SimConnectService` survive pipe failures without orphaning flights, eliminate unsynchronized concurrent access to the SimConnect handle, and split `CameraState` onto its own wall-clock-polled definition so bulk flight data can move to a `SIM_FRAME` subscription.

**Architecture:** Four pure decision units are extracted from `SimConnectService` into testable classes under `FSTRaK/BusinessLogic/SimconnectService/` — a connection-recovery mapper, a flight-state transition table, and a flight-identity check. The service keeps its properties, timers, and side effects but delegates every decision to those units. Data flow changes from one 20 Hz polled stream to two streams: a 250 ms camera poll that survives sim pause, and a `SIM_FRAME` `FlightData` subscription that does not.

**Tech Stack:** C# / .NET Framework 4.7.2, WPF, x64. SimConnect managed SDK. xUnit 2.9.2 for tests (`FSTRaK.Tests`). Serilog for logging.

**Spec:** `docs/superpowers/specs/2026-08-25-simconnect-resilience-design.md`

## Global Constraints

- Target framework `net472`, platform **x64 only** — SimConnect has a native DLL dependency; x86/AnyCPU will not build or run.
- **No build or test execution is possible on the development machine (macOS).** Every task's "run the test" step is performed by the user on Windows, or deferred to a batch verification run. Do not claim a test passed without output.
- Project file to edit is `FSTRaK/FSTrAk.csproj`. Git also tracks an alias `FSTRaK.csproj`; after committing, sync it with `git update-index` — a plain `git add` fails on it.
- New files must be added to `FSTRaK/FSTrAk.csproj` explicitly — this is an old-style (non-SDK) csproj that does not glob source files. `FSTRaK.Tests.csproj` is SDK-style and does glob; test files need no csproj edit.
- Tests follow the existing style in `FSTRaK.Tests`: xUnit `[Fact]`/`[Theory]`, plain constructed structs, no mocking framework.
- Grace period on pipe loss: **60 seconds**. Camera poll interval: **250 ms**.
- Identity thresholds: **30 nm airborne**, **20 nm on ground**.
- Version ships as **3.7.5**.

---

## File Structure

**Create:**
- `FSTRaK/BusinessLogic/SimconnectService/ConnectionRecovery.cs` — maps an HRESULT to a recovery action.
- `FSTRaK/BusinessLogic/SimconnectService/FlightStateEvaluator.cs` — pure in-flight transition table.
- `FSTRaK/BusinessLogic/SimconnectService/FlightIdentity.cs` — resume-or-end decision after reconnect.
- `FSTRaK.Tests/ConnectionRecoveryTests.cs`
- `FSTRaK.Tests/FlightStateEvaluatorTests.cs`
- `FSTRaK.Tests/FlightIdentityTests.cs`

**Modify:**
- `FSTRaK/DataTypes/SimConnectDataTypes.cs` — add `CameraData` struct, extend `DataDefinitions` and `Requests`, remove `CameraState` from `FlightData`.
- `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs` — locking, recovery, grace timer, stream split.
- `FSTRaK/BusinessLogic/FlightManager/FlightManager.cs` — move `HandleFlightExitEvent`, expose last-known position and identity state.
- `FSTRaK/FSTrAk.csproj` — register the three new source files.
- `FSTRaK/Properties/AssemblyInfo.cs`, `Setup/Setup.vdproj`, `RELEASE_NOTES.md`, `README.md`, `docs/index.html`, `docs/project-overview.md` — version bump.

**Task order rationale:** Tasks 1–3 are pure, fully testable, and touch nothing existing — they can be verified in isolation. Tasks 4–6 wire them into the service. Task 7 is the data-flow split, which depends on 4–6 being in place. Task 8 is the version bump, last so release notes describe what shipped.

---

### Task 1: Connection recovery mapper

**Files:**
- Create: `FSTRaK/BusinessLogic/SimconnectService/ConnectionRecovery.cs`
- Test: `FSTRaK.Tests/ConnectionRecoveryTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum RecoveryAction { Reconnect, LogOnly }` and
  `static RecoveryAction ConnectionRecovery.ActionFor(uint hresult)`,
  `static string ConnectionRecovery.DescribeFor(uint hresult)`.

- [ ] **Step 1: Write the failing test**

Create `FSTRaK.Tests/ConnectionRecoveryTests.cs`:

```csharp
using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class ConnectionRecoveryTests
    {
        [Theory]
        [InlineData(0xC00000B0u)] // STATUS_PIPE_DISCONNECTED - the primary defect
        [InlineData(0xC000014Bu)] // STATUS_PIPE_BROKEN
        [InlineData(0x800706BAu)] // RPC_S_SERVER_UNAVAILABLE
        [InlineData(0x80004005u)] // E_FAIL
        [InlineData(0xDEADBEEFu)] // unrecognized
        public void ActionFor_AnyComError_Reconnects(uint hresult)
        {
            Assert.Equal(RecoveryAction.Reconnect, ConnectionRecovery.ActionFor(hresult));
        }

        [Fact]
        public void DescribeFor_KnownCodes_AreDistinct()
        {
            var pipeDisconnected = ConnectionRecovery.DescribeFor(0xC00000B0u);
            var pipeBroken = ConnectionRecovery.DescribeFor(0xC000014Bu);
            var rpcUnavailable = ConnectionRecovery.DescribeFor(0x800706BAu);

            Assert.NotEqual(pipeDisconnected, pipeBroken);
            Assert.NotEqual(pipeBroken, rpcUnavailable);
            Assert.NotEqual(pipeDisconnected, rpcUnavailable);
        }

        [Fact]
        public void DescribeFor_UnknownCode_MentionsTheCode()
        {
            var description = ConnectionRecovery.DescribeFor(0xDEADBEEFu);

            Assert.Contains("DEADBEEF", description, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter ConnectionRecoveryTests`
Expected: FAIL — compile error, `ConnectionRecovery` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `FSTRaK/BusinessLogic/SimconnectService/ConnectionRecovery.cs`:

```csharp
namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// What the service should do when a COMException surfaces from a SimConnect call.
    /// </summary>
    internal enum RecoveryAction
    {
        /// <summary>Tear down the handle and start reconnecting.</summary>
        Reconnect,

        /// <summary>Record the error only; the connection remains usable.</summary>
        LogOnly
    }

    /// <summary>
    /// Maps COM error codes from SimConnect calls onto a recovery action.
    ///
    /// Every COMException reconnects. An error that leaves the pipe usable is the rare
    /// case, and treating an unknown code as recoverable is what produced the zombie
    /// state this class exists to prevent: the old handler recognised only
    /// STATUS_PIPE_BROKEN, so a STATUS_PIPE_DISCONNECTED left the data timer polling a
    /// dead pipe indefinitely. Failing closed costs a reconnect; failing open costs the
    /// flight.
    /// </summary>
    internal static class ConnectionRecovery
    {
        private const uint StatusPipeDisconnected = 0xC00000B0;
        private const uint StatusPipeBroken = 0xC000014B;
        private const uint RpcServerUnavailable = 0x800706BA;
        private const uint EFail = 0x80004005;

        public static RecoveryAction ActionFor(uint hresult)
        {
            return RecoveryAction.Reconnect;
        }

        public static string DescribeFor(uint hresult)
        {
            switch (hresult)
            {
                case StatusPipeDisconnected:
                    return "The simulator closed the SimConnect pipe (STATUS_PIPE_DISCONNECTED).";
                case StatusPipeBroken:
                    return "The SimConnect pipe is broken (STATUS_PIPE_BROKEN).";
                case RpcServerUnavailable:
                    return "The RPC server is unavailable (RPC_S_SERVER_UNAVAILABLE).";
                case EFail:
                    return "SimConnect reported an unspecified failure (E_FAIL).";
                default:
                    return $"Unrecognised SimConnect COM error 0x{hresult:X8}.";
            }
        }
    }
}
```

`ActionFor` ignoring its argument is deliberate, not a stub: the decision is
uniform today, and the parameter is the seam for adding a `LogOnly` code later
without changing any call site.

- [ ] **Step 4: Add the file to the project**

In `FSTRaK/FSTrAk.csproj`, find the `<ItemGroup>` containing
`<Compile Include="BusinessLogic\SimconnectService\SimConnectService.cs" />` and add
immediately above it:

```xml
    <Compile Include="BusinessLogic\SimconnectService\ConnectionRecovery.cs" />
```

- [ ] **Step 5: Run test to verify it passes**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter ConnectionRecoveryTests`
Expected: PASS, 7 tests.

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/ConnectionRecovery.cs FSTRaK.Tests/ConnectionRecoveryTests.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: map SimConnect COM errors to a recovery action"
git update-index --add --cacheinfo 100644 "$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj)" FSTRaK/FSTRaK.csproj
```

---

### Task 2: Flight-state transition table

**Files:**
- Create: `FSTRaK/BusinessLogic/SimconnectService/FlightStateEvaluator.cs`
- Test: `FSTRaK.Tests/FlightStateEvaluatorTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: `FSTRaK.DataTypes.CameraState`.
- Produces: `static bool FlightStateEvaluator.IsInFlight(FlightStateInputs inputs)` and the
  `FlightStateInputs` struct with fields `CameraState Camera`, `CameraState PreviousCamera`,
  `uint PauseState`, `string LoadedFlight`, `bool IsConnected`, `bool WasInFlight`.

This task reproduces the exact behaviour of the current `UpdateInFlightState` method
(`SimConnectService.cs:504-558`), with one change: the disconnect branch becomes
reachable. Read that method before starting; the branch order below is significant
and must be preserved.

- [ ] **Step 1: Write the failing test**

Create `FSTRaK.Tests/FlightStateEvaluatorTests.cs`:

```csharp
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using Xunit;

namespace FSTRaK.Tests
{
    public class FlightStateEvaluatorTests
    {
        private const string MainMenuFlt = "flights\\other\\MainMenu.FLT";

        private static FlightStateInputs Inputs(
            CameraState camera = CameraState.Cockpit,
            CameraState previousCamera = CameraState.Cockpit,
            uint pauseState = 0,
            string loadedFlight = "flights\\other\\SomeFlight.FLT",
            bool isConnected = true,
            bool wasInFlight = false)
        {
            return new FlightStateInputs
            {
                Camera = camera,
                PreviousCamera = previousCamera,
                PauseState = pauseState,
                LoadedFlight = loadedFlight,
                IsConnected = isConnected,
                WasInFlight = wasInFlight
            };
        }

        [Theory]
        [InlineData(CameraState.Cockpit)]
        [InlineData(CameraState.External)]
        [InlineData(CameraState.Drone)]
        [InlineData(CameraState.Fixed)]
        [InlineData(CameraState.Environment)]
        [InlineData(CameraState.SixDof)]
        [InlineData(CameraState.FollowTrafficAircraft)]
        public void LiveCamera_IsInFlight(CameraState camera)
        {
            Assert.True(FlightStateEvaluator.IsInFlight(Inputs(camera: camera)));
        }

        [Theory]
        [InlineData(CameraState.LoadingFlight3D2024)]
        [InlineData(CameraState.SomethingInLoadingProcess2024)]
        public void LoadingCamera_IsNotInFlight(CameraState camera)
        {
            Assert.False(FlightStateEvaluator.IsInFlight(Inputs(camera: camera)));
        }

        [Fact]
        public void MainMenu2024_EndsFlight()
        {
            var inputs = Inputs(
                camera: CameraState.MainMenu2024,
                previousCamera: CameraState.Cockpit,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void MainMenu2024_FromInFlightMenu_DoesNotEndFlight()
        {
            // Guards a 2024 quirk: the main-menu camera appears transiently when
            // leaving the in-flight menu, and must not be read as ending the flight.
            var inputs = Inputs(
                camera: CameraState.MainMenu2024,
                previousCamera: CameraState.InFlightMenu2024_3,
                wasInFlight: true);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Theory]
        [InlineData(CameraState.InFlightMenu2024, 1u)]
        [InlineData(CameraState.InFlightMenu2024_2, 8u)]
        [InlineData(CameraState.InFlightMenu2024_3, 0u)]
        public void InFlightMenuWhileInFlight_StaysInFlight(CameraState camera, uint pauseState)
        {
            // Entering VR or active pause mid-flight must not end the flight.
            var inputs = Inputs(camera: camera, pauseState: pauseState, wasInFlight: true);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void PauseState9WhileInFlight_EndsFlight()
        {
            var inputs = Inputs(
                camera: CameraState.GamePlay,
                pauseState: 9,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void Disconnected_EndsFlight()
        {
            // Regression: previously unreachable, because after a disconnect none of the
            // properties that triggered re-evaluation ever changed again.
            var inputs = Inputs(
                camera: CameraState.Cockpit,
                isConnected: false,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void LoadedFlightNotMainMenu_IsInFlight()
        {
            var inputs = Inputs(camera: CameraState.GamePlay, pauseState: 0);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void MainMenuFlt_IsNotInFlight()
        {
            var inputs = Inputs(camera: CameraState.MenuRtc, loadedFlight: MainMenuFlt);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(8u)]
        [InlineData(9u)]
        public void PausedWithNoLiveCamera_IsNotInFlight(uint pauseState)
        {
            var inputs = Inputs(camera: CameraState.GamePlay, pauseState: pauseState);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void EmptyLoadedFlight_IsNotInFlight()
        {
            var inputs = Inputs(camera: CameraState.MenuRtc, loadedFlight: string.Empty);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter FlightStateEvaluatorTests`
Expected: FAIL — compile error, `FlightStateEvaluator` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `FSTRaK/BusinessLogic/SimconnectService/FlightStateEvaluator.cs`:

```csharp
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Everything the in-flight decision depends on, captured at one instant.
    /// </summary>
    internal struct FlightStateInputs
    {
        public CameraState Camera;
        public CameraState PreviousCamera;
        public uint PauseState;
        public string LoadedFlight;
        public bool IsConnected;
        public bool WasInFlight;
    }

    /// <summary>
    /// Decides whether the simulator is in a flight.
    ///
    /// The branch order below is load-bearing and was arrived at empirically against both
    /// MSFS 2020 and 2024; the two sims signal flight entry and exit differently, and
    /// several branches exist to suppress transient states that would otherwise read as a
    /// flight ending. Change the order only with a test that pins the behaviour you intend.
    /// </summary>
    internal static class FlightStateEvaluator
    {
        private const string MainMenuFlt = "flights\\other\\MainMenu.FLT";

        public static bool IsInFlight(FlightStateInputs inputs)
        {
            // A lost connection ends the flight regardless of the last camera seen.
            if (inputs.WasInFlight && !inputs.IsConnected)
            {
                return false;
            }

            // Active pause and the in-flight menu (including entering VR) must not end a
            // flight already under way. PauseState 8 also follows 9 when 2024 ends a flight.
            if (inputs.WasInFlight
                && (inputs.PauseState == 1 || inputs.PauseState == 8 || inputs.PauseState == 0)
                && (inputs.Camera == CameraState.InFlightMenu2024
                    || inputs.Camera == CameraState.InFlightMenu2024_2
                    || inputs.Camera == CameraState.InFlightMenu2024_3))
            {
                return true;
            }

            // A live camera means the aircraft is being flown - the 2024 entry condition.
            if (inputs.Camera == CameraState.Cockpit
                || inputs.Camera == CameraState.External
                || inputs.Camera == CameraState.Drone
                || inputs.Camera == CameraState.Fixed
                || inputs.Camera == CameraState.Environment
                || inputs.Camera == CameraState.SixDof
                || inputs.Camera == CameraState.FollowTrafficAircraft)
            {
                return true;
            }

            if (inputs.Camera == CameraState.LoadingFlight3D2024
                || inputs.Camera == CameraState.SomethingInLoadingProcess2024)
            {
                return false;
            }

            if (inputs.Camera == CameraState.MainMenu2024)
            {
                // 2024 shows the main-menu camera transiently on the way out of the
                // in-flight menu; that is not a flight ending.
                return inputs.PreviousCamera == CameraState.InFlightMenu2024_3;
            }

            if (inputs.WasInFlight && inputs.PauseState == 9)
            {
                return false;
            }

            // 2020 entry condition: a loaded flight that is not the main menu, unpaused.
            return !string.IsNullOrEmpty(inputs.LoadedFlight)
                   && !inputs.LoadedFlight.Equals(MainMenuFlt)
                   && inputs.PauseState != 1
                   && inputs.PauseState != 8
                   && inputs.PauseState != 9;
        }
    }
}
```

- [ ] **Step 4: Add the file to the project**

In `FSTRaK/FSTrAk.csproj`, next to the `ConnectionRecovery.cs` entry added in Task 1:

```xml
    <Compile Include="BusinessLogic\SimconnectService\FlightStateEvaluator.cs" />
```

- [ ] **Step 5: Run test to verify it passes**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter FlightStateEvaluatorTests`
Expected: PASS, 22 tests.

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/FlightStateEvaluator.cs FSTRaK.Tests/FlightStateEvaluatorTests.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: extract flight-state transition table as pure logic"
git update-index --add --cacheinfo 100644 "$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj)" FSTRaK/FSTRaK.csproj
```

---

### Task 3: Flight identity check

**Files:**
- Create: `FSTRaK/BusinessLogic/SimconnectService/FlightIdentity.cs`
- Test: `FSTRaK.Tests/FlightIdentityTests.cs`
- Modify: `FSTRaK/FSTrAk.csproj`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `static bool FlightIdentity.CanResume(FlightIdentitySnapshot before, FlightIdentitySnapshot after, bool isInFlight, bool isMsfs2024)` and the
  `FlightIdentitySnapshot` struct with fields `string Title`, `string LiveryName`,
  `double Latitude`, `double Longitude`, `bool OnGround`.

- [ ] **Step 1: Write the failing test**

Create `FSTRaK.Tests/FlightIdentityTests.cs`:

```csharp
using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class FlightIdentityTests
    {
        // One degree of latitude is 60 nm, so 0.5 degrees is 30 nm along a meridian.
        private const double OneNauticalMileInDegrees = 1.0 / 60.0;

        private static FlightIdentitySnapshot Snapshot(
            string title = "Airbus A320neo",
            string livery = "Lufthansa",
            double latitude = 40.0,
            double longitude = -74.0,
            bool onGround = false)
        {
            return new FlightIdentitySnapshot
            {
                Title = title,
                LiveryName = livery,
                Latitude = latitude,
                Longitude = longitude,
                OnGround = onGround
            };
        }

        [Fact]
        public void SameAircraftNearby_Resumes()
        {
            var before = Snapshot();
            var after = Snapshot(latitude: 40.0 + (5 * OneNauticalMileInDegrees));

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void NotInFlight_DoesNotResume()
        {
            // The user quit to the menu during the gap.
            var before = Snapshot();
            var after = Snapshot();

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: false, isMsfs2024: true));
        }

        [Fact]
        public void DifferentTitle_DoesNotResume()
        {
            var before = Snapshot(title: "Airbus A320neo");
            var after = Snapshot(title: "Cessna 172");

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void DifferentLiveryOn2024_DoesNotResume()
        {
            var before = Snapshot(livery: "Lufthansa");
            var after = Snapshot(livery: "Air France");

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void DifferentLiveryOn2020_Resumes()
        {
            // MSFS 2020 does not report a usable livery, so it must not gate the decision.
            var before = Snapshot(livery: "Lufthansa");
            var after = Snapshot(livery: "");

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: false));
        }

        [Fact]
        public void TitleComparison_IgnoresSurroundingWhitespace()
        {
            var before = Snapshot(title: "Airbus A320neo ");
            var after = Snapshot(title: " Airbus A320neo");

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void Airborne25NauticalMiles_Resumes()
        {
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: false);

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void Airborne35NauticalMiles_DoesNotResume()
        {
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (35 * OneNauticalMileInDegrees),
                onGround: false);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void OnGround25NauticalMiles_DoesNotResume()
        {
            // The ground threshold is tighter: 30 nm could span a neighbouring airport.
            var before = Snapshot(onGround: true);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void OnGround15NauticalMiles_Resumes()
        {
            var before = Snapshot(onGround: true);
            var after = Snapshot(
                latitude: 40.0 + (15 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.True(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void GroundThresholdFollowsTheReconnectedSample()
        {
            // Airborne before, on the ground after: the post-reconnect sample decides,
            // so the tighter ground threshold applies.
            var before = Snapshot(onGround: false);
            var after = Snapshot(
                latitude: 40.0 + (25 * OneNauticalMileInDegrees),
                onGround: true);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }

        [Fact]
        public void TeleportAcrossTheWorld_DoesNotResume()
        {
            var before = Snapshot(latitude: 40.0, longitude: -74.0);
            var after = Snapshot(latitude: 51.5, longitude: -0.12);

            Assert.False(FlightIdentity.CanResume(before, after, isInFlight: true, isMsfs2024: true));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter FlightIdentityTests`
Expected: FAIL — compile error, `FlightIdentity` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `FSTRaK/BusinessLogic/SimconnectService/FlightIdentity.cs`:

```csharp
using System;
using System.Device.Location;
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// The aircraft and position at one instant, used to decide whether a flight survived
    /// a connection gap.
    /// </summary>
    internal struct FlightIdentitySnapshot
    {
        public string Title;
        public string LiveryName;
        public double Latitude;
        public double Longitude;
        public bool OnGround;
    }

    /// <summary>
    /// Decides whether the session seen after a reconnect is the same flight that was
    /// under way before the connection dropped.
    ///
    /// The airborne tolerance is the looser of the two deliberately: restarting a flight,
    /// getting airborne and arriving within 30 nm of the last known position inside the
    /// grace window is implausible, whereas a 30 nm radius on the ground could easily
    /// span a neighbouring airport.
    /// </summary>
    internal static class FlightIdentity
    {
        private const double AirborneToleranceNauticalMiles = 30.0;
        private const double GroundToleranceNauticalMiles = 20.0;

        public static bool CanResume(
            FlightIdentitySnapshot before,
            FlightIdentitySnapshot after,
            bool isInFlight,
            bool isMsfs2024)
        {
            if (!isInFlight)
            {
                return false;
            }

            if (!TitlesMatch(before.Title, after.Title))
            {
                return false;
            }

            // Only MSFS 2024 reports a livery that distinguishes variants of one title.
            if (isMsfs2024 && !TitlesMatch(before.LiveryName, after.LiveryName))
            {
                return false;
            }

            var tolerance = after.OnGround
                ? GroundToleranceNauticalMiles
                : AirborneToleranceNauticalMiles;

            return DistanceInNauticalMiles(before, after) <= tolerance;
        }

        private static bool TitlesMatch(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static double DistanceInNauticalMiles(
            FlightIdentitySnapshot before,
            FlightIdentitySnapshot after)
        {
            var from = new GeoCoordinate(before.Latitude, before.Longitude);
            var to = new GeoCoordinate(after.Latitude, after.Longitude);
            return from.GetDistanceTo(to) * Consts.MetersToNauticalMiles;
        }
    }
}
```

`Consts.MetersToNauticalMiles` is defined in `FSTRaK/DataTypes/Consts.cs` (namespace
`FSTRaK.DataTypes`) and is used the same way in `FlightManager.cs:191` and
`FlightEndedState.cs:89`. Do not redefine the constant locally.

- [ ] **Step 4: Add the file to the project**

In `FSTRaK/FSTrAk.csproj`, next to the entries added in Tasks 1 and 2:

```xml
    <Compile Include="BusinessLogic\SimconnectService\FlightIdentity.cs" />
```

- [ ] **Step 5: Run test to verify it passes**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj --filter FlightIdentityTests`
Expected: PASS, 12 tests.

- [ ] **Step 6: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/FlightIdentity.cs FSTRaK.Tests/FlightIdentityTests.cs FSTRaK/FSTrAk.csproj
git commit -m "feat: decide flight identity across a connection gap"
git update-index --add --cacheinfo 100644 "$(git rev-parse HEAD:FSTRaK/FSTrAk.csproj)" FSTRaK/FSTRaK.csproj
```

---

### Task 4: Synchronize SimConnect handle access

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: a `_simConnectLock` field and a `SafeSimConnectCall` helper that later tasks
  reuse for every handle access.

No unit test — this is concurrency inside a class that cannot be instantiated without a
live SimConnect handle. Verified manually in Task 8.

- [ ] **Step 1: Add the lock field**

In `SimConnectService.cs`, next to the existing `private SimConnect _simconnect = null;`
declaration, add:

```csharp
    /// <summary>
    /// Guards every access to <see cref="_simconnect"/>. Three threads reach the handle:
    /// the UI thread via WndProc, the camera timer, and the connection timer. Critical
    /// sections must stay narrow - never hold this across a property change, because
    /// those reach the FlightManager state machine, which calls back into this service.
    /// </summary>
    private readonly object _simConnectLock = new object();
```

- [ ] **Step 2: Add the guarded-call helper**

Add this private method to `SimConnectService`:

```csharp
    /// <summary>
    /// Runs a SimConnect call under the handle lock, routing any failure through the
    /// recovery path. NullReference and ObjectDisposed are caught alongside COMException
    /// because a teardown can still race a caller that captured the handle.
    /// </summary>
    private void SafeSimConnectCall(Action<SimConnect> call, string description)
    {
        try
        {
            lock (_simConnectLock)
            {
                if (_simconnect == null)
                {
                    Log.Debug($"Skipping {description} - no SimConnect handle.");
                    return;
                }

                call(_simconnect);
            }
        }
        catch (COMException ex)
        {
            HandleCOMException(ex);
        }
        catch (Exception ex) when (ex is NullReferenceException || ex is ObjectDisposedException)
        {
            Log.Warning(ex, $"SimConnect handle disposed during {description}; reconnecting.");
            HandleConnectionLost();
        }
    }
```

- [ ] **Step 3: Route the request methods through the helper**

Replace the bodies of `RequestNearestAirport`, `RequestLoadedAircraft`, and
`RequestFlightData` so each uses the helper. For example, `RequestLoadedAircraft`
becomes:

```csharp
    public void RequestLoadedAircraft()
    {
        SafeSimConnectCall(sc =>
        {
            sc.RequestDataOnSimObject(Requests.AircraftDataRequest, DataDefinitions.AircraftData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0u, 0u, 0u);
            sc.RequestSystemState(Requests.AircraftLoaded, "AircraftLoaded");
        }, nameof(RequestLoadedAircraft));
    }
```

Apply the same shape to `RequestNearestAirport` (keeping its
`NearestAirportDistance = double.MaxValue;` assignment before the call) and to
`RequestFlightData`. `RequestFlightData` is deleted in Task 7; convert it here anyway so
the intermediate state compiles and runs.

- [ ] **Step 4: Guard ReceiveMessage in WndProc**

Replace the body of the `if (msg == WmUserSimconnect && _simconnect != null)` block so the
lock wraps only `ReceiveMessage()`:

```csharp
        if (msg == WmUserSimconnect)
        {
            SafeSimConnectCall(sc => sc.ReceiveMessage(), "ReceiveMessage");
            handled = true;
        }
```

The `_simconnect != null` test moves inside the helper, where it is read under the lock.

- [ ] **Step 5: Replace the Synchronized attribute on Close**

Remove `[MethodImpl(MethodImplOptions.Synchronized)]` from `Close()` and lock explicitly:

```csharp
    public void Close()
    {
        _cameraTimer?.Stop();

        lock (_simConnectLock)
        {
            if (_simconnect != null)
            {
                _simconnect.Dispose();
                _simconnect = null;
            }
        }

        Log.Debug("SimConnect Disposed!");
    }
```

`_cameraTimer` is named `_dataTimer` until Task 7; use whichever name currently exists so
this task compiles on its own. Remove the now-unused `using System.Runtime.CompilerServices;`
if nothing else in the file needs it.

- [ ] **Step 6: Guard the connection path**

In `ConnectToSimulator`, wrap the construction and configuration together:

```csharp
    private void ConnectToSimulator()
    {
        try
        {
            Log.Debug("Trying to connect to the simulator...");

            lock (_simConnectLock)
            {
                _simconnect = new SimConnect("FSTrAk", _lHwnd, WmUserSimconnect, null, 0);

                try
                {
                    ConfigureSimconnect();
                }
                catch (Exception ex)
                {
                    // A half-configured handle would otherwise be retained forever, and
                    // WaitForSimConnection only restarts the timer when the field is null.
                    Log.Error(ex, "SimConnect configuration failed; discarding the handle.");
                    _simconnect.Dispose();
                    _simconnect = null;
                    throw;
                }
            }
        }
        catch (COMException ex)
        {
            Log.Debug(ex, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected failure while connecting to the simulator.");
        }

        if (_simconnect == null && _connectionTimer != null && !_connectionTimer.Enabled)
        {
            _connectionTimer.Start();
        }
    }
```

This closes the Task-4 half of the configuration leak; `ConfigureSimconnect` itself calls
methods on `_simconnect` while the lock is already held, which is safe because `lock` is
reentrant on the same thread.

- [ ] **Step 7: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs
git commit -m "fix: synchronize all SimConnect handle access"
```

---

### Task 5: Connection loss, recovery, and the grace period

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs`

**Interfaces:**
- Consumes: `ConnectionRecovery.ActionFor`, `ConnectionRecovery.DescribeFor` (Task 1);
  `SafeSimConnectCall`, `_simConnectLock` (Task 4).
- Produces: `HandleConnectionLost()`, a `_gracePeriodTimer`, and a public
  `bool IsAwaitingReconnect { get; }` that Task 6 reads.

- [ ] **Step 1: Add the grace-period constant and timer**

Next to the existing `ConnectionInterval` constant, add:

```csharp
    /// <summary>
    /// How long an in-progress flight survives a dropped pipe before it is ended. Long
    /// enough to ride out a transient blip, short enough that a flight does not sit open
    /// after the user closes the simulator and walks away.
    /// </summary>
    private const int GracePeriodInterval = 60000;
```

And next to the other timer fields:

```csharp
    private Timer _gracePeriodTimer;
```

- [ ] **Step 2: Add the awaiting-reconnect property**

```csharp
    private bool _isAwaitingReconnect;

    /// <summary>
    /// True between a pipe failure and either a successful reconnect or the grace period
    /// expiring. The active flight is held in limbo while this is set.
    /// </summary>
    public bool IsAwaitingReconnect
    {
        get => _isAwaitingReconnect;
        private set
        {
            if (value != _isAwaitingReconnect)
            {
                _isAwaitingReconnect = value;
                OnPropertyChanged();
            }
        }
    }
```

- [ ] **Step 3: Create the grace timer in Initialize**

Add a `SetGracePeriodTimer()` method and call it from `Initialize()` alongside
`SetConnectionTimer()`:

```csharp
    private void SetGracePeriodTimer()
    {
        _gracePeriodTimer = new Timer(GracePeriodInterval);
        _gracePeriodTimer.AutoReset = false;
        _gracePeriodTimer.Elapsed += (sender, e) => OnGracePeriodExpired();
    }

    private void OnGracePeriodExpired()
    {
        Log.Information($"No reconnection within {GracePeriodInterval / 1000}s - ending the flight.");
        IsAwaitingReconnect = false;
        IsInFlight = false;
    }
```

- [ ] **Step 4: Add the connection-lost handler**

```csharp
    /// <summary>
    /// Tears down a failed connection and starts reconnecting. An active flight is kept
    /// alive for the grace period so a transient pipe failure does not discard it.
    /// </summary>
    private void HandleConnectionLost()
    {
        var hadFlight = IsInFlight;

        StopGettingData();
        Close();
        IsConnected = false;
        SimVersion = null;

        if (hadFlight)
        {
            IsAwaitingReconnect = true;
            _gracePeriodTimer.Stop();
            _gracePeriodTimer.Start();
            Log.Information($"Connection lost mid-flight; holding the flight for {GracePeriodInterval / 1000}s.");
        }
        else
        {
            IsInFlight = false;
        }

        _connectionTimer.Start();
    }
```

- [ ] **Step 5: Rewrite HandleCOMException**

Replace the whole method:

```csharp
    private void HandleCOMException(COMException ex)
    {
        var hresult = (uint)ex.ErrorCode;
        Log.Error(ex, $"COMException: {ConnectionRecovery.DescribeFor(hresult)} (HRESULT: 0x{hresult:X8})");

        if (ConnectionRecovery.ActionFor(hresult) == RecoveryAction.Reconnect)
        {
            HandleConnectionLost();
        }
    }
```

- [ ] **Step 6: Fix the quit handler and the dead exception branch**

`simconnect_OnRecvQuit` ends the flight immediately — a clean quit is unambiguous, so it
gets no grace period:

```csharp
    private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log.Information("Connection to the simulator is closed!");

        StopGettingData();
        Close();
        IsConnected = false;
        SimVersion = null;
        IsAwaitingReconnect = false;
        _gracePeriodTimer.Stop();
        IsInFlight = false;   // was missing: a flight in progress was orphaned, never saved
        _connectionTimer.Start();
    }
```

`simconnect_OnRecvException` logs the protocol-level exception without tearing down. The
old HRESULT comparison could never be true, because `dwException` carries a
`SIMCONNECT_EXCEPTION` enum value:

```csharp
    private void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        var exception = (SIMCONNECT_EXCEPTION)data.dwException;

        // Protocol-level errors - a bad SimVar or an unknown request - do not indicate a
        // failed pipe, so they must not trigger a teardown.
        Log.Error($"SimConnect exception {exception} on send id {data.dwSendID}, index {data.dwIndex}");
    }
```

- [ ] **Step 7: Cancel the grace period on reconnect**

In `simconnect_OnRecvOpen`, before the existing version request:

```csharp
        _gracePeriodTimer.Stop();

        if (IsAwaitingReconnect)
        {
            // Cancelled by reconnection, not by a completed identity check: the first data
            // sample may arrive well after the window would have closed, and a flight we
            // successfully reconnected to must not be lost waiting for it.
            Log.Information("Reconnected inside the grace window; verifying flight identity.");
            _pendingIdentityCheck = true;
            RequestLoadedAircraft();
        }
```

Declare the flag alongside the other fields:

```csharp
    private bool _pendingIdentityCheck;
```

- [ ] **Step 8: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs
git commit -m "fix: recover from pipe loss and stop orphaning flights on sim exit"
```

---

### Task 6: Wire the evaluator and identity check into the service

**Files:**
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs`
- Modify: `FSTRaK/BusinessLogic/FlightManager/FlightManager.cs`

**Interfaces:**
- Consumes: `FlightStateEvaluator.IsInFlight`, `FlightStateInputs` (Task 2);
  `FlightIdentity.CanResume`, `FlightIdentitySnapshot` (Task 3); `IsAwaitingReconnect`,
  `_pendingIdentityCheck` (Task 5).
- Produces: `FlightManager.LastKnownSnapshot` — a `FlightIdentitySnapshot` of the most
  recent in-flight sample, read by the service when verifying identity.

- [ ] **Step 1: Replace UpdateInFlightState's body with the evaluator**

```csharp
    private void UpdateInFlightState()
    {
        var inputs = new FlightStateInputs
        {
            Camera = CameraState,
            PreviousCamera = PreviousCameraState,
            PauseState = PauseState,
            LoadedFlight = LoadedFlight,
            IsConnected = IsConnected,
            WasInFlight = IsInFlight
        };

        var isInFlight = FlightStateEvaluator.IsInFlight(inputs);

        Log.Debug($"Flight state: pause {PauseState}, sim started {SimStarted}, camera {CameraState} -> in flight {isInFlight}");

        // While awaiting reconnection the flight is held; the grace timer or the identity
        // check decides its fate, not the stale camera state left behind by the drop.
        if (IsAwaitingReconnect && !isInFlight)
        {
            return;
        }

        IsInFlight = isInFlight;
    }
```

The two `Log.Information` calls become one `Log.Debug`: the line is diagnostic and fires
on every camera transition.

- [ ] **Step 2: Track the last known snapshot on FlightManager**

In `FlightManager.cs`, add the property:

```csharp
        /// <summary>
        /// Aircraft and position from the most recent in-flight sample, used to decide
        /// whether a flight survived a connection gap.
        /// </summary>
        public FlightIdentitySnapshot LastKnownSnapshot { get; private set; }
```

Populate it in the `nameof(SimConnectService.FlightData)` case, inside the existing
`if (State is not SimNotInFlightState)` block, after `CurrentFlightParams` is assigned:

```csharp
                        LastKnownSnapshot = new FlightIdentitySnapshot
                        {
                            Title = ActiveFlight?.Aircraft?.Title,
                            LiveryName = ActiveFlight?.Aircraft?.LiveryName,
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            OnGround = Convert.ToBoolean(data.SimOnGround)
                        };
```

Add `using FSTRaK.BusinessLogic.SimconnectService;` to the file if not already present.

- [ ] **Step 3: Run the identity check on the first post-reconnect sample**

In `SimConnectService.Simconnect_OnRecvSimobjectData`, in the `FlightDataRequest` branch,
after `FlightData` is assigned:

```csharp
                if (_pendingIdentityCheck)
                {
                    _pendingIdentityCheck = false;
                    VerifyFlightIdentity(FlightData);
                }
```

And add the method:

```csharp
    /// <summary>
    /// Decides whether the reconnected session is the same flight. Runs on the first data
    /// sample after a reconnect, because aircraft data and position are not available at
    /// the moment the connection opens.
    /// </summary>
    private void VerifyFlightIdentity(FlightData data)
    {
        var before = FlightManager.FlightManager.Instance.LastKnownSnapshot;

        var after = new FlightIdentitySnapshot
        {
            Title = AircraftData.title,
            LiveryName = AircraftData.liveryName,
            Latitude = data.Latitude,
            Longitude = data.Longitude,
            OnGround = Convert.ToBoolean(data.SimOnGround)
        };

        var canResume = FlightIdentity.CanResume(
            before, after, IsInFlight, SimVersion == MSFS2024);

        IsAwaitingReconnect = false;

        if (canResume)
        {
            Log.Information($"Resuming the flight after reconnection - {after.Title} near the last known position.");
        }
        else
        {
            Log.Information($"Not the same flight after reconnection (was {before.Title}, now {after.Title}); ending it.");
            IsInFlight = false;
        }
    }
```

Add `using System;` for `Convert` if not already present.

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs FSTRaK/BusinessLogic/FlightManager/FlightManager.cs
git commit -m "feat: verify flight identity after reconnecting"
```

---

### Task 7: Split CameraState onto its own polled definition

**Files:**
- Modify: `FSTRaK/DataTypes/SimConnectDataTypes.cs`
- Modify: `FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs`
- Modify: `FSTRaK/BusinessLogic/FlightManager/FlightManager.cs`

**Interfaces:**
- Consumes: everything from Tasks 4–6.
- Produces: the final data-flow shape. No new public API.

This is the highest-risk task. `FlightData`'s field order must match its
`AddToDataDefinition` call order exactly — removing `CameraState` from one without the
other silently misaligns every field after it, which manifests as nonsense flight data
rather than an error.

- [ ] **Step 1: Add the new definition, request, and struct**

In `FSTRaK/DataTypes/SimConnectDataTypes.cs`:

```csharp
    public enum Requests
    {
        FlightDataRequest,
        NearbyAirportsRequest,
        FlightLoaded,
        AircraftLoaded,
        AircraftDataRequest,
        SimVersionRequest,
        CameraDataRequest
    }

    public enum DataDefinitions
    {
        AircraftData,
        FlightData,
        CameraData
    }
```

New members are appended, never inserted — these are marshalled by ordinal value.

And the struct, next to `FlightData`:

```csharp
    /// <summary>
    /// Camera state on its own definition so it can be polled on a wall clock. It is the
    /// primary flight start and exit signal, and must stay observable while the simulator
    /// is paused, in a menu, or loading - exactly when a SIM_FRAME subscription is silent.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct CameraData
    {
        public CameraState CameraState;
    }
```

- [ ] **Step 2: Remove CameraState from FlightData**

In the `FlightData` struct, delete the line `public CameraState CameraState;` (it sits
between `VerticalSpeed` and `FlapSpeedExceeded`).

- [ ] **Step 3: Remove the matching data definition registration**

In `ConfigureSimconnect`, delete the paired registration:

```csharp
        _simconnect.AddToDataDefinition(DataDefinitions.FlightData, "Camera State", null, SIMCONNECT_DATATYPE.INT32,
            0.0f, SimConnect.SIMCONNECT_UNUSED);
```

Verify by eye that the remaining `AddToDataDefinition` calls for `FlightData` are in the
same order as the struct's fields, `zuluYear` through `BankDegrees`.

- [ ] **Step 4: Register the camera definition and the SIM_FRAME subscription**

In `ConfigureSimconnect`, after the existing `RegisterDataDefineStruct` calls:

```csharp
        _simconnect.AddToDataDefinition(DataDefinitions.CameraData, "Camera State", null,
            SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simconnect.RegisterDataDefineStruct<CameraData>(DataDefinitions.CameraData);
```

Replace the trailing `StartGettingData();` with the standing subscription followed by the
camera poll:

```csharp
        // One standing subscription replaces the former 50ms request loop. SIM_FRAME is
        // tied to the physics loop rather than the render loop, so it does not fluctuate
        // with GPU load - and it stops while the simulator is paused, which is why camera
        // state is polled separately.
        _simconnect.RequestDataOnSimObject(Requests.FlightDataRequest, DataDefinitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SIM_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);

        StartGettingData();
```

- [ ] **Step 5: Convert the data timer into a camera timer**

Rename the constant, field, and methods:

```csharp
    private const int CameraPollInterval = 250;
```

```csharp
    private Timer _cameraTimer;
```

```csharp
    private void SetCameraTimer()
    {
        _cameraTimer = new Timer(CameraPollInterval);
        _cameraTimer.Elapsed += (sender, e) => RequestCameraData();
        _cameraTimer.AutoReset = true;
    }

    private void StartGettingData()
    {
        _cameraTimer.Start();
    }

    private void StopGettingData()
    {
        _cameraTimer?.Stop();
    }
```

Update `Initialize()` to call `SetCameraTimer()` in place of `SetDataTimer()`, and
`Close()` to stop `_cameraTimer`.

- [ ] **Step 6: Replace RequestFlightData with RequestCameraData**

Delete `RequestFlightData()` entirely — the subscription replaces it — and add:

```csharp
    /// <summary>
    /// Polls camera state on a wall clock. Deliberately not part of the FlightData
    /// subscription: this must keep arriving while the simulator is paused or in a menu.
    /// </summary>
    public void RequestCameraData()
    {
        SafeSimConnectCall(sc =>
        {
            sc.RequestDataOnSimObject(Requests.CameraDataRequest, DataDefinitions.CameraData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);
        }, nameof(RequestCameraData));
    }
```

- [ ] **Step 7: Handle the camera delivery**

In `Simconnect_OnRecvSimobjectData`, remove `CameraState = FlightData.CameraState;` from
the `FlightDataRequest` branch and add a new branch:

```csharp
            else if (data.dwRequestID == (int)Requests.CameraDataRequest)
            {
                var cameraData = (CameraData)data.dwData[0];
                CameraState = cameraData.CameraState;
                FlightManager.FlightManager.Instance.HandleCameraTick();
            }
```

- [ ] **Step 8: Move flight-exit handling onto the camera tick**

In `FlightManager.cs`, remove `State.HandleFlightExitEvent();` from the
`nameof(SimConnectService.FlightData)` case, and add the method:

```csharp
        /// <summary>
        /// Driven by the camera poll rather than the flight-data stream, so that leaving a
        /// flight is still detected while the simulator is paused - which is exactly when
        /// the SIM_FRAME subscription stops delivering. This also closes a case where
        /// pausing after landing and then quitting left a pending touchdown unfinalized.
        /// </summary>
        public void HandleCameraTick()
        {
            State.HandleFlightExitEvent();
        }
```

- [ ] **Step 9: Commit**

```bash
git add FSTRaK/DataTypes/SimConnectDataTypes.cs FSTRaK/BusinessLogic/SimconnectService/SimConnectService.cs FSTRaK/BusinessLogic/FlightManager/FlightManager.cs
git commit -m "feat: poll camera state separately and subscribe FlightData at SIM_FRAME"
```

---

### Task 8: Manual verification and version bump

**Files:**
- Modify: `FSTRaK/Properties/AssemblyInfo.cs:57-58`
- Modify: `Setup/Setup.vdproj:3998-4006`
- Modify: `RELEASE_NOTES.md`, `README.md`, `docs/index.html`, `docs/project-overview.md`

**Interfaces:**
- Consumes: a complete, verified implementation from Tasks 1–7.
- Produces: the 3.7.5 release.

- [ ] **Step 1: Run the full unit test suite**

On Windows: `dotnet test FSTRaK.Tests/FSTRaK.Tests.csproj`
Expected: PASS, including the pre-existing `TouchdownTrackerTests`, `FlightScoreTests`,
`StatisticsCalculationsTests`, `AirlineResolverTests`, and `SimBriefOfpMapperTests`.

- [ ] **Step 2: Build and run against MSFS**

Build `Release|x64` in Visual Studio and work through each case. Every one must pass
before the version is bumped; record the observed behaviour.

1. **Normal flight** — start, taxi, take off, land, taxi in, park. The flight records and
   saves as before, with a plausible score.
2. **Quit to main menu mid-flight** — the flight ends.
3. **Pause after landing, then quit** — the landing is finalized with its G force, not
   dropped.
4. **Kill MSFS mid-flight** (Task Manager) — the flight ends within a second or two
   rather than hanging; check the log for the quit handler.
5. **Kill MSFS mid-flight, restart, reload the same aircraft at the same airport inside
   60 s** — the log shows "Resuming the flight after reconnection".
6. **Kill MSFS mid-flight, restart, load a different aircraft or a distant airport inside
   60 s** — the log shows "Not the same flight after reconnection".
7. **Kill MSFS mid-flight and wait past 60 s** — the log shows the grace period expiring
   and the flight ends.
8. **Observe delivery rate and UI responsiveness.** Watch the live map and Flight Data
   card during cruise. If the UI is visibly heavier than before, the `SIM_FRAME` rate is
   higher than estimated; note the behaviour and stop before bumping the version. The
   fallback is a throttle on map updates, not a revert.

- [ ] **Step 3: Bump the assembly version**

In `FSTRaK/Properties/AssemblyInfo.cs`, lines 57-58:

```csharp
[assembly: AssemblyVersion("3.7.5.0")]
[assembly: AssemblyFileVersion("3.7.5.0")]
```

- [ ] **Step 4: Generate two fresh installer GUIDs**

In PowerShell: `[guid]::NewGuid().ToString().ToUpper()` twice. Use the first for
`ProductCode`, the second for `PackageCode`.

- [ ] **Step 5: Bump the installer version**

In `Setup/Setup.vdproj`, update three lines and leave `UpgradeCode` untouched:

```
        "ProductCode" = "8:{FIRST-NEW-GUID}"
        "PackageCode" = "8:{SECOND-NEW-GUID}"
        "ProductVersion" = "8:3.7.5"
```

A changed `ProductVersion` without a fresh `ProductCode` breaks the Windows Installer
major-upgrade path, so an existing install would not be replaced cleanly. This is the
pattern commit `23fdab7` followed for 3.7.0.

- [ ] **Step 6: Update the release notes and docs**

Add a 3.7.5 entry at the top of `RELEASE_NOTES.md`, following the format of the existing
entries:

```markdown
## 3.7.5

### Fixed
- Flights are no longer lost when the simulator closes or the SimConnect connection
  drops mid-flight. A dropped connection now holds the flight for 60 seconds and
  resumes it if the same aircraft reconnects near the last known position.
- Recovered from SimConnect pipe errors that previously left the app polling a dead
  connection indefinitely.
- Leaving a flight while the simulator is paused is now detected reliably, including
  finalizing a landing when pausing immediately after touchdown.

### Changed
- Flight data now arrives on a SimConnect subscription tied to the simulator's physics
  loop instead of a 50 ms polling loop, reducing inter-process traffic.
```

Update the version string in `README.md`, `docs/index.html`, and
`docs/project-overview.md` wherever 3.7.0 appears as the current version. Search first:
`grep -rn "3\.7\.0" README.md docs/index.html docs/project-overview.md`

- [ ] **Step 7: Commit**

```bash
git add FSTRaK/Properties/AssemblyInfo.cs Setup/Setup.vdproj RELEASE_NOTES.md README.md docs/index.html docs/project-overview.md
git commit -m "chore: version 3.7.5 - SimConnect resilience"
```

---

## Self-Review

**Spec coverage.** Section 1 (synchronization) → Task 4. Section 2 (connection loss and
recovery) → Tasks 1 and 5. Section 3 (flight identity) → Tasks 3 and 6. Section 4
(CameraState split) → Task 7. Section 5 (transition table extraction) → Tasks 2 and 6.
Version bump → Task 8. Testing → unit tests in Tasks 1–3, manual checklist in Task 8. No
gaps.

**Type consistency.** `FlightIdentitySnapshot` is constructed in Tasks 3, 6, and 7 with
the same five fields throughout. `FlightStateInputs` carries six fields, all supplied at
its single call site. `RecoveryAction` is produced in Task 1 and consumed in Task 5.
`StartGettingData`/`StopGettingData` keep their names across the Task 7 rename so the
Task 5 call sites stay valid. `HandleConnectionLost` is defined in Task 5 and referenced
by the Task 4 helper — Task 4's code therefore does not compile standalone; the two tasks
must land together, or a stub added and replaced.

**Known ordering caveat.** Task 4 references `HandleConnectionLost` (Task 5) and
`_cameraTimer` (Task 7). Implementers should either run Tasks 4 and 5 as one commit, or
add a temporary `HandleConnectionLost` that calls the existing teardown, and replace it in
Task 5. Flagged rather than reordered, because splitting the lock work from the recovery
work keeps each reviewable on its own.
