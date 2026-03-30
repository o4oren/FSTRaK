# Client Detail Panel — Design Spec

**Date:** 2026-03-30
**Status:** Approved

---

## Overview

When a pilot or ATC marker is clicked on the live map, a rich glassmorphism detail panel appears in the bottom-right corner of the live view. It stays open until dismissed (close button, Escape, or clicking empty map space). A styled compact tooltip appears on hover. Selected pilots have their flight path drawn on the map.

---

## 1. Selection State

### `SelectedClientViewModel`

A new class held as a nullable property `SelectedClient` on `LiveViewViewModel`. Null when nothing is selected.

**Properties:**
- `ClientType` — enum: `Pilot`, `ATC`
- `Network` — enum: `VATSIM`, `IVAO`
- `IsOwnAircraft` — bool; true when the client's CID matches the user's configured CID and the relevant network feed is active
- `IsOwnAircraftInFlight` — bool; true when `IsOwnAircraft` and `FlightManager` is in an active flight state
- Raw data reference (VATSIM `Pilot` / IVAO `IvaoPilot` / VATSIM `Controller` / IVAO ATC session)
- `TrackPoints` — `List<TrackPoint>` (lat/lon/alt/timestamp)
- All computed display properties (see panel sections below)

### Selection lifecycle

- **Set**: clicking a map marker fires a command on `LiveViewViewModel` that constructs and sets `SelectedClient`
- **Update**: on each 60s poll, if `SelectedClient` is non-null, the ViewModel finds the matching callsign+network in the fresh data and updates the wrapper in-place (so the panel reflects live data)
- **Clear**: close button (×), Escape key, or clicking empty map space sets `SelectedClient = null` and removes flight path lines

### Own aircraft suppression rule

- If the user has a VATSIM or IVAO CID configured in Settings and that CID appears in the active feed: the network marker is **shown and clickable** when the user is **not** in an active MSFS flight
- When the user **is** in an active MSFS flight (FlightManager in any active state), the network marker is **hidden** — the SimConnect aircraft is the interaction point instead
- Clicking the SimConnect aircraft while in flight opens the same panel, merging SimConnect real-time data with the network flight plan data

---

## 2. Position History and Flight Path

### Track accumulation

A `Dictionary<string, List<TrackPoint>>` keyed by callsign is maintained in `LiveViewViewModel`. On every poll cycle, each visible pilot's current position is appended. Points are discarded when the callsign disappears from the feed. This runs for all visible pilots (not just selected), so the trail is pre-populated on click.

`TrackPoint`:
```csharp
record TrackPoint(double Latitude, double Longitude, int Altitude, DateTime Timestamp);
```

### IVAO track fetch

When an IVAO pilot is selected, a one-time fetch is made to:
```
GET https://api.ivao.aero/v2/tracker/sessions/{userId}/tracks
```
This replaces/pre-populates `TrackPoints` with the full session history. Subsequent poll cycles continue appending `lastTrack`.

### VATSIM track

VATSIM has no public track API at this time. StatSim (`statsim.net`) requires an API key and TOS review — **deferred**. Local accumulation (poll-cycle appending) is the only source.

### Map rendering

Two `MapPolyline` elements are added directly to the `Map` control's children (not via `MapItemsControl`) when `SelectedClient` is set:

1. **Solid polyline** — renders `TrackPoints`. Color: network accent (VATSIM: #38bdf8, IVAO: #FF8C00). Not shown when `IsOwnAircraftInFlight`.
2. **Dashed polyline** — geodesic line from current position to destination airport lat/lon. Rendered as a series of interpolated points. Shown for all pilots including own aircraft in flight.

Both polylines are removed when `SelectedClient` is cleared.

---

## 3. Hover Tooltip

Styled using a WPF `ControlTemplate` on the existing `ToolTip` controls. Dark semi-transparent background (`#CC1a2a3a`), 1px border (`#33ffffff`), 8px corner radius. Positions near the cursor via standard WPF ToolTip placement.

**Pilot tooltip:**
- Callsign (bold)
- DEP → ARR
- Aircraft type
- Altitude (ft) · Groundspeed (kts)

**Airport ATC tooltip:**
- ICAO code (bold) + airport name
- Row of active facility badges (TWR, GND, DEL, APP, DEP — colored pills)
- Frequencies per active controller

**CTR / APP / FSS tooltip:**
- Callsign (bold)
- Position type label
- Frequency

---

## 4. Client Detail Panel — `ClientDetailPanelControl`

A WPF UserControl placed as a bottom-right overlay inside `LiveView.xaml`. `Visibility` bound to `SelectedClient != null` (collapsed when null). Fixed width ~260px, variable height.

### Visual style

Glassmorphism: dark semi-transparent background (`rgba(255,255,255,0.07)` equivalent via WPF layering), subtle border (`#22ffffff`), 12px corner radius. A blurred dark rectangle rendered behind the panel simulates the frosted glass effect (WPF `BitmapEffect` or `RenderTargetBitmap` blur on a background snapshot).

### 4a. Pilot Panel

**Header row:**
- Network logo (small 16×16 image — VATSIM or IVAO asset)
- Airline logo (40×40, loaded from `/Assets/AirlineLogos/{ICAO3}.png` where ICAO3 = first 3 chars of callsign; fallback: generic plane icon)
- Callsign (bold, 16px)
- Pilot name · CID (muted, 10px)
- IFR/VFR badge (colored pill)

**Route bar:**
- DEP ICAO (bold, blue) — airport name below (muted)
- Progress line: solid left portion + dashed right portion, live aircraft icon at % position
- ARR ICAO (bold, muted blue) — airport name below
- Second row: "63% · 1,420 nm remaining · ETA 2h 14m"
- Progress bar (thin, gradient)

**Stats grid (3 columns):**
| Altitude (ft) | Groundspeed (kts) | Heading (°) |
| Aircraft type | Squawk | Online time |

**Route string** (monospace, truncated with ellipsis, full text on hover)

**Remarks** (muted, truncated)

**Own aircraft in flight variant:** Stats (altitude, speed, heading) sourced from SimConnect. Flight plan fields (dep, arr, aircraft, route, remarks) from network data. A small "LIVE" badge next to the network logo indicates the sim data is active.

### 4b. Airport ATC Panel

**Header row:**
- Network logo
- Airport ICAO (bold, large) + airport name
- Row of active facility type badges (colored pills): TWR=green, GND=yellow, DEL=purple, APP=cyan, DEP=cyan, CTR=orange, ATIS=gray

**Controller table** (one row per active controller):
- Callsign · Position type · Frequency · Rating · Online time

**ATIS section:**
- VATSIM: shown from the ATIS controller's `text_atis`, collapsed by default, expandable
- IVAO: "ATIS unavailable" placeholder (deferred — requires IVAO auth)

### 4c. CTR / APP / FSS Panel

**Header row:**
- Network logo
- Facility type badge (CTR / APP / FSS)
- Callsign (bold)
- Controller name · CID

**Stats row:**
- Frequency (large, prominent)
- Rating · Online time · Visual range

**ATIS section:** same rules as airport panel

### Close behavior
- × button top-right clears `SelectedClient`
- Escape key binding on `LiveView`
- Click on empty map area (no marker hit) clears `SelectedClient`

---

## 5. Airline Logo Assets

A new `/Assets/AirlineLogos/` directory in the project. PNGs keyed by 3-letter ICAO airline code (e.g. `ELY.png`, `DAL.png`). Sourced from the same set VatView uses (`/app/assets/logos/` — ~163 airlines). An `AirlineLogoResolver` utility class handles the lookup with a fallback to a generic icon.

---

## 6. Settings Interactions

- **User CID fields** (VATSIM CID, IVAO User ID) already exist in Settings — used for own-aircraft detection
- **"Show my aircraft on map" toggle** — existing setting; when off, own aircraft network marker is fully hidden and not selectable. The SimConnect aircraft (shown when in flight) is unaffected by this toggle and remains clickable.
- No new settings needed for this feature

---

## 7. Out of Scope

- StatSim VATSIM track API (deferred — needs API key and TOS review)
- IVAO ATIS (deferred — requires IVAO authentication investigation)
- Clicking to show other users' flight logbook entries
- Any changes to the Logbook or Statistics views
