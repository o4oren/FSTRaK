# Sim AI Traffic Layer — Design

**Date:** 2026-09-25
**Status:** Approved for planning
**Branch:** `feat/sim-ai-traffic`

## Problem

Users have asked to see AI aircraft on the live map. FSTRaK already renders
online-network traffic — VATSIM and IVAO clients fetched over HTTP and drawn as
`MapItemsControl` layers — but nothing in the application reads *sim-local*
objects out of SimConnect. Whatever is flying around you inside MSFS is
invisible to the app.

## Goals

- Draw the aircraft and helicopters the simulator knows about on the live map.
- Match the existing traffic experience: rotated icon, tooltip, clickable
  detail panel.
- Cost nothing when switched off.
- Stay clear of the SIM_FRAME flight-data path stabilised in PR #57.

## Non-goals

- Ground vehicles and boats. Airborne vehicle types only (including those
  currently on the ground).
- Track history or trails for traffic aircraft. SimConnect exposes no route or
  history for AI objects.
- User-configurable radius, poll rate, or filters. No new settings.
- Refactoring the existing nested `VatsimAicraft` / `IvaoAircraft` classes.
  Unrelated to this goal.

## Scope decisions

**Object types.** SimConnect `AIRCRAFT` and `HELICOPTER`. This covers
sim-injected AI traffic and real-world live traffic, which MSFS materialises as
AI objects.

**Multiplayer caveat.** Human multiplayer players are *inconsistently* visible
through SimConnect and frequently report generic titles. Whatever the simulator
exposes will be drawn; full multiplayer coverage is best-effort and is not a
success criterion for this work.

**Relationship to VATSIM/IVAO.** Sim traffic is an independent layer with its
own toggle, not a third member of the network selector. It can be shown at the
same time as a network layer. No duplicate suppression between the two — if a
user's own VATSIM traffic is also injected into the sim, both will draw.

**Volume.** Fixed 80 NM radius, 2 s poll. No setting.

## Architecture

### Acquisition

SimConnect's `RequestDataOnSimObjectType` is a one-shot request — it returns
every object of a type within a radius of the user in a single burst, and no
`PERIOD` can be attached to it. Keeping a list current therefore requires a
timer. Two alternatives were considered and rejected:

- *Per-object subscriptions* (poll for discovery, then
  `RequestDataOnSimObject(id, PERIOD.SECOND)` per object). Smoother, but it
  means managing N live subscriptions with churn as traffic enters and leaves
  the radius, N× the message volume through the same `WndProc` pump as flight
  data, and a large blast radius on a subsystem that was just stabilised.
- *Client-side dead reckoning* between polls. Smooth without extra sim traffic,
  but it draws aircraft where they are not, and needs its own render tick.

Snapshot polling was chosen. Icons step rather than glide between polls; this is
acceptable — the VATSIM layer updates every 15 s and is not felt as a problem.
Dead reckoning remains available as a later refinement if the stepping proves
irritating in practice.

### Components

#### `DataTypes/SimConnectDataTypes.cs` (modified)

New struct `SimTrafficData`, laid out per the existing convention
(`LayoutKind.Sequential`, `CharSet.Ansi`, `Pack = 1`):

| Field | SimVar | Type |
|---|---|---|
| `Title` | `Title` | `STRING256` |
| `AtcId` | `ATC ID` | `STRING32` |
| `AtcType` | `ATC Type` | `STRING256` |
| `Airline` | `ATC Airline` | `STRING256` |
| `FlightNumber` | `ATC Flight Number` | `STRING32` |
| `Category` | `Category` | `STRING128` |
| `Latitude` | `Plane Latitude` (degrees) | `FLOAT64` |
| `Longitude` | `Plane Longitude` (degrees) | `FLOAT64` |
| `Altitude` | `Plane Altitude` (feet) | `FLOAT64` |
| `TrueHeading` | `Plane Heading Degrees True` (degrees) | `FLOAT64` |
| `GroundVelocity` | `Ground Velocity` (knots) | `FLOAT64` |
| `SimOnGround` | `Sim On Ground` (Bool) | `INT32` |

New `Requests` members: `SimTrafficAircraftRequest`,
`SimTrafficHelicopterRequest`. New `DataDefinitions` member: `SimTrafficData`.

#### `BusinessLogic/SimconnectService/SimTrafficTracker.cs` (new)

Owns everything about traffic tracking except the SimConnect calls themselves.
Holds no SimConnect handle, and is therefore unit-testable in isolation — the
same shape as `NotificationGate`.

Responsibilities:

- A `System.Timers.Timer` at 2 s, **created stopped**, independent of the
  SIM_FRAME subscription, the camera timer, and the connection timer.
- Batch assembly. By-type replies arrive one message per object, each carrying
  `dwentrynumber` and `dwoutof`. Entries accumulate into a pending batch; the
  snapshot is published only when `dwentrynumber == dwoutof`, so the UI never
  observes a half-filled list. Aircraft and helicopter batches merge into one
  snapshot.
- Staleness. A poll cycle that returns nothing produces no messages at all from
  SimConnect. If two consecutive cycles elapse with no batch completing, an
  empty snapshot is published, clearing the map.
- Torn batches. A batch still pending when the next cycle's first entry arrives
  is discarded, never merged — a snapshot must never mix two cycles.
- User exclusion. By-type results include the user's own aircraft. The tracker
  records the `dwObjectID` observed on the existing user `FlightDataRequest`
  replies and excludes exactly that ID. If no user object ID has been observed
  yet — possible in the first moments after connect — the tracker publishes
  nothing rather than risk drawing a duplicate of the user's aircraft.
- Exposes `TrafficUpdated`, carrying an immutable list.

#### `BusinessLogic/SimconnectService/SimConnectService.cs` (modified)

Remains the only component touching the SimConnect handle.

- Registers the `SimTrafficData` definition and struct in `ConfigureSimconnect`.
- Subscribes `OnRecvSimobjectDataBytype` and forwards entries to the tracker.
- On each tracker tick, issues two `RequestDataOnSimObjectType` calls
  (`SIMCONNECT_SIMOBJECT_TYPE.AIRCRAFT`, `SIMCONNECT_SIMOBJECT_TYPE.HELICOPTER`)
  at radius 148,160 m (80 NM), both wrapped in the existing
  `SafeSimConnectCall`.
- Records the user's `dwObjectID` from `FlightDataRequest` replies and supplies
  it to the tracker.
- `HandleConnectionLost` stops the traffic timer and clears the snapshot
  alongside its existing work.

#### `ViewModels/SimTrafficViewModel.cs` (new)

Owns the `BindingList<SimTrafficAircraft>`, the `IsShowSimTraffic` toggle, and
the `TrafficUpdated` subscription. On each snapshot it rebuilds the list and
calls the existing `ReplaceContent` extension, as `ProcessVatsimPilots` does.

`LiveViewViewModel` exposes it as a single property (`SimTraffic`). That file is
already 1840 lines and this is its third traffic source; the additions there are
a handful of lines rather than another block of layer logic.

#### `ViewModels/SimTrafficAircraft.cs` (new)

The map item model, in its own file rather than nested the way `VatsimAicraft`
is. Properties: object ID, callsign, type, airline, `Location`, heading,
altitude, ground speed, on-ground flag, `IconResource` and `ScaleFactor` from
`AircraftResolver.GetAircraftIcon(atcType)`, and `TooltipText` in the
established multi-line shape.

Callsign resolution, in order: `ATC ID`, then `ATC Flight Number`, then `Title`.
First non-empty wins.

#### `Views/LiveView.xaml` (modified)

A new `MapItemsControl` bound to `SimTraffic.Aircraft`, using the same
`ItemContainerStyle` and rotated `Path` template as the network layers, with the
same `OnMapItemClicked` event setter.

Z-order: placed **below** the IVAO and VATSIM layers in the file — sim traffic
under network traffic, both under the user's own aircraft — preserving the
"Vatsim Aircraft (top network layer)" ordering already commented there.

A new `ToggleButton` in the right-hand `StackPanel`, bound to
`SimTraffic.IsShowSimTraffic`. Unlike the Pilots and ATC buttons, which key off
`IsAnyNetworkActive`, it is visible whenever SimConnect is connected and has no
relationship to the network selector.

#### `Resources/Theme.xaml`, `Resources/DarkTheme.xaml` (modified)

A new `SimTrafficAircraftColorBrush`, visually distinct from the VATSIM blue and
the IVAO orange.

#### `ViewModels/SelectedClientViewModel.cs` (modified)

A new constructor taking a `SimTrafficAircraft`. `Network` stays
`NetworkType.None` — the enum is unchanged. `ClientKind` is `Pilot` so the
existing pilot layout applies. A new `IsSimTraffic` flag drives visibility in
`ClientDetailPanelControl.xaml`.

Shown: callsign, type, airline, altitude, ground speed, heading, airborne /
on-ground.

Hidden, because SimConnect exposes none of it for AI objects: CID, rating,
flight rules, departure/arrival, route, ETA and progress, ATIS, track trail.

## Lifecycle

`IsShowSimTraffic` starts `false` on every launch. It is **not persisted** —
consistent with the decision to add no settings.

Polling runs only while all three hold:

1. the toggle is on,
2. SimConnect is connected,
3. the sim is started.

Toggling off stops the timer and clears the list, so no objects linger on the
map. Losing the sim connection stops the timer and clears; reconnecting restarts
polling only if the toggle is still on.

## Failure behaviour

- SimConnect calls go through `SafeSimConnectCall`. An exception on a traffic
  request is logged and swallowed; the traffic layer degrades on its own rather
  than tearing down the connection.
- An incomplete batch is discarded, never merged into the next cycle.
- With no user object ID observed yet, nothing is published.
- Two silent cycles clear the map rather than leaving stale aircraft frozen in
  place.

## Testing

`SimTrafficTracker` holds no SimConnect handle and is unit-tested in
`FSTRaK.Tests`, following `NotificationGateTests`:

- a complete batch publishes exactly once;
- a partial batch publishes nothing;
- two silent cycles publish an empty snapshot;
- the user's object ID is excluded;
- no user object ID observed ⇒ nothing published;
- a torn batch is discarded rather than merged;
- aircraft and helicopter batches merge into a single snapshot.

`SimTrafficAircraft` is tested for callsign fallback (ATC ID → flight number →
title) and icon resolution.

The map layer, toggle, and detail panel are WPF and are verified manually.

**Verification constraint:** development happens on macOS, where this solution
cannot be built or run. All code lands unverified and is built and tested on
Windows by the maintainer. No claim of working behaviour will be made from the
development machine.

## Success criteria

- With the toggle on and MSFS running, AI aircraft and helicopters within 80 NM
  appear on the live map, oriented to their heading, refreshing every 2 s.
- The user's own aircraft is never drawn twice.
- Clicking a traffic aircraft opens the detail panel with the fields SimConnect
  provides, and no empty network-only fields.
- With the toggle off — its default — no traffic requests are issued at all.
- Flight tracking, scoring, and the state machine are unaffected.
