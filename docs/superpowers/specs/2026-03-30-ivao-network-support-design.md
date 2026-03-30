# IVAO Network Support — Design Spec
**Date:** 2026-03-30
**Status:** Approved

## Overview

Add IVAO live network viewing to FSTRaK's live map, parallel to the existing VATSIM implementation. Users can see IVAO pilots and ATC positions on the map, switching between VATSIM and IVAO via a network selector. One network is active at a time.

## Scope

- IVAO pilots on the live map
- IVAO ATC positions on the live map (all ATC types combined — airports, FIRs, UIRs — no separate layers)
- Network selector UI (VATSIM / IVAO) replacing the current three VATSIM-specific toggle buttons
- Unified Pilots and ATC toggles (ATC merges the existing VATSIM Airports + FIRs toggles in the UI only — data structures unchanged)
- IVAO ID setting to exclude the user's own aircraft
- Polling lifecycle: start on network select, stop on network deselect or both toggles off

**Out of scope:** Track history, simultaneous multi-network display, per-facility ATC filters.

---

## New Files

### `FSTRaK/BusinessLogic/IvaoService/`

**`IvaoService.cs`**
Singleton, mirrors `VatsimService`. Polls both IVAO endpoints every 60 seconds. Exposes `IvaoData` property via `INotifyPropertyChanged`. No static boundary files required — ATC control polygons are included in the API response.

API endpoints:
- `https://api.ivao.aero/v2/tracker/now/pilots/summary`
- `https://api.ivao.aero/v2/tracker/now/atc/summary`

Both are fetched in the same polling tick. A single `IvaoData` object is assembled and `PropertyChanged` is fired once.

**`IvaoModel/IvaoPilot.cs`**
Maps from pilots/summary response. Fields: `userId`, `callsign`, `latitude`, `longitude`, `altitude`, `heading`, `groundspeed`, `lastTrack` (contains flight plan info).

**`IvaoModel/IvaoAtcSession.cs`**
Maps from atc/summary response. Fields: `userId`, `callsign`, `frequency`, `atcSession` (contains `position` type, `logonTime`, `textAtis`), `lastTrack` (contains control polygon geometry).

**`IvaoModel/IvaoData.cs`**
Container: `List<IvaoPilot> clients`, `List<IvaoAtcSession> atcPositions`.

---

## Modified Files

### `FSTRaK/ViewModels/LiveViewViewModel.cs`

**New enum** — add to `FSTRaK/DataTypes/` as a new file `NetworkType.cs`:
```csharp
public enum NetworkType { None, Vatsim, Ivao }
```

**New properties:**
- `ActiveNetwork` — `NetworkType`, drives selector UI state and routing of toggle actions
- `IsShowPilots` — unified pilots toggle (replaces `IsShowVatsimAircraft` as the XAML binding; internally sets the appropriate VATSIM/IVAO visibility)
- `IsShowAtc` — unified ATC toggle (replaces `IsShowVatsimAirports` + `IsShowVatsimFirs` as XAML bindings)
- `IvaoAircraftList` — `BindingList<IvaoAircraft>` (new nested class, same pattern as `VatsimAicraft`)
- `IvaoAtcList` — `BindingList<IvaoAtcPosition>` (new nested class for ATC map items)

**Existing VATSIM properties preserved** (`IsShowVatsimAircraft`, `IsShowVatsimAirports`, `IsShowVatsimFirs`) — driven internally by `IsShowPilots`/`IsShowAtc` when `ActiveNetwork == Vatsim`, not bound directly in XAML.

**New commands:**
- `SelectNetworkCommand` (parameter: `NetworkType`)
  - If parameter equals `ActiveNetwork`: deselect — stop service, clear collections, set `ActiveNetwork = None`, set `IsShowPilots = false`, `IsShowAtc = false`
  - Otherwise: stop previous service and clear its collections, set `ActiveNetwork`, reset `IsShowPilots = false` and `IsShowAtc = false`, start new service (immediate fetch + timer)
- `EnableNetworkItemCommand` / `DisableNetworkItemCommand` — replaces existing Vatsim-specific commands, routes to correct service based on `ActiveNetwork`

**New handler:** `IvaoServiceOnPropertyChanged` — mirrors `VatsimServiceOnPropertyChanged`, calls `ProcessIvaoPilots()` and `ProcessIvaoAtc()`.

**New processing methods:**
- `ProcessIvaoPilots()` — maps `IvaoPilot` → `IvaoAircraft`, filters out user's IVAO ID (from `Properties.Settings.Default.IvaoId`)
- `ProcessIvaoAtc()` — maps `IvaoAtcSession` → `IvaoAtcPosition`, renders control polygon from geometry in the response

### `FSTRaK/Views/LiveView.xaml`

**Toggle panel (right side) — replace existing 3 VATSIM toggles with:**
1. Network selector: two `ToggleButton`s (VATSIM / IVAO), each bound to `SelectNetworkCommand` with respective `NetworkType` parameter. Active network button appears highlighted. Clicking the active one deselects.
2. **Pilots** `ToggleButton` — bound to `IsShowPilots`, visible only when `ActiveNetwork != None`
3. **ATC** `ToggleButton` — bound to `IsShowAtc`, visible only when `ActiveNetwork != None`

**Map layers:**
- Existing VATSIM layers (`VatsimAircraftList`, `VatsimControlledAirports`, `VatsimControlledFirs`, `VatsimControlledUirs`) — visibility gated on `IsShowPilots`/`IsShowAtc` AND `ActiveNetwork == Vatsim`
- New IVAO layers: `IvaoAircraftList` (same aircraft path/icon template as VATSIM), `IvaoAtcList` (polygon + icon, same pattern as VATSIM airports)

### `FSTRaK/Views/SettingsView.xaml`

Add IVAO ID text field below the existing VATSIM ID field.

### `FSTRaK/ViewModels/SettingsViewModel.cs`

Add `IvaoId` property, persisted to `Properties.Settings.Default.IvaoId`.

### `FSTRaK/Properties/Settings.settings`

Add `IvaoId` string setting (default empty string).

---

## Data Flow

### Network selection
1. User clicks network selector button
2. `SelectNetworkCommand` fires:
   - If re-clicking active network: stop service, clear collections, `ActiveNetwork = None`, uncheck both toggles
   - If switching: stop previous service + clear its collections, set `ActiveNetwork`, call immediate fetch + start timer
3. Pilots/ATC toggles become visible

### Toggle interaction
- Toggling Pilots or ATC **off**: clear relevant collection(s), service keeps running
- Both Pilots and ATC **off**: stop service (network remains selected in UI)
- Either toggle re-enabled when service stopped: restart service

### Polling
- `IvaoService` fetches both endpoints per tick, assembles `IvaoData`, fires single `PropertyChanged`
- `LiveViewViewModel` handles the change, routes to `ProcessIvaoPilots()` and/or `ProcessIvaoAtc()` based on toggle state

---

## Settings

| Setting | Key | Type | Default |
|---------|-----|------|---------|
| VATSIM ID | `VatsimId` | string | "" |
| IVAO ID | `IvaoId` | string | "" |

---

## Documentation Updates

### README.md

Update the Features section to reflect current state and IVAO addition:

```markdown
## Features
* Automatic silent start-up.
* Automatic flight tracking (hands-free experience).
* Option (default) to save only complete flights - i.e. flight ended in parking state, with engines off and parking brake set, after having flown.
* Multiple map providers including FAA ArcGIS charts, OpenAIP, OpenTopoMap, Bing, and more.
* Flight analysis and scoring.
* Live flight tracking with a moving map.
* VATSIM and IVAO live network support — view pilots and ATC on the live map.
```

Update the Roadmap to move VATSIM/IVAO off the todo list:

```markdown
## Roadmap
- [ ] Simbrief integration (fetch passengers, planned vs actual fuel and time, planned vs actual route).
- [ ] Statistics (on time, passengers flown, best/worst landings, average fps, average score).
- [x] VATSIM integration (display live traffic and ATC on the map).
- [x] IVAO integration (display live traffic and ATC on the map).
```

### flightsim.to listing

The flightsim.to page could not be fetched automatically (403). The owner should update the listing description to reflect:

1. **Multi-network support** — mention both VATSIM and IVAO by name
2. **Map providers** — the listing likely still references outdated map options; update to reflect current providers (FAA ArcGIS, OpenAIP, SkyVector VFR/IFR, OpenTopoMap, Bing, MapTiler, AIP Israel)
3. **MSFS 2024** — confirm compatibility with both MSFS 2020 and MSFS 2024

Full updated listing copy (replace existing flightsim.to description entirely):

---

**Description**

FSTrAk is a modern flight tracker and logbook for MSFS 2020 and 2024.

FSTrAk aims to be a no-frills, install-and-let-it-run experience — it requires no manual intervention. FSTrAk monitors your simulator silently, detects when a flight is started, tracks it on a map, and persists it to a local database when it is complete.

---

**Installation**

Unzip the file and run the installer.

---

**Database**

FSTrAk saves data as an SQLite database called FSTrAk.db in your %appdata% folder (usually C:\Users\[username]\AppData\Local\FSTRaK).
You can back up this file. Although upgrading and uninstalling does not delete this folder, backing up the file periodically is recommended.

---

**Features**

- Automatic silent start-up
- Automatic flight tracking (hands-free experience)
- Option (default) to save only complete flights — i.e. flight ended in parking state, with engines off and parking brake set, after having flown
- Live flight tracking with a moving map
- **VATSIM and IVAO live network support** — switch between networks to see pilots and ATC positions on the live map in real time
- Multiple map providers:
  - OpenStreetMap
  - OpenTopoMap
  - FAA ArcGIS sectional and IFR enroute charts
  - SkyVector VFR and IFR maps
  - OpenAIP
  - Azure Maps
  - TopPlus Open (BKG)
  - MapTiler
  - AIP Israel
- Flight analysis and scoring
- Statistics (most used aircraft, most used airlines, average and max payload, distance, etc.)
- Dark mode

---

**Roadmap**

- Simbrief integration (fetch passengers, planned vs actual fuel and time, planned vs actual route)
- More statistics
- Display bearing/distance to a designated point on the map

---

FSTrAk uses icons from Airport icons created by Freepik - Flaticon

---

## Constraints

- No automated tests possible in this environment (Mac, WPF/.NET Framework project)
- No static boundary files for IVAO — all geometry comes from the API
- ATC toggle merges Airports + FIRs/UIRs at the UI layer only; VATSIM data structures and processing methods are unchanged
