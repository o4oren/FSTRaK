# MSFS Moving Map Tablet Panel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a self-contained MSFS 2024 Community addon that shows a moving map panel inside the sim, consuming tiles and ATC data from FSTRaK's local tile server.

**Architecture:** The panel is a single `index.html` with Leaflet.js inlined — no build step, no CDN. It reads tiles from `http://localhost:8765/` and aircraft position from SimVars. FSTRaK's `/network/state` endpoint is extended to include controlled airports. The addon folder is committed to the repo and included in the distribution.

**Tech Stack:** HTML5, CSS3, vanilla JS, Leaflet.js 1.9.4 (inlined), Python 3 (layout generator), C# .NET Framework 4.7.2 (NetworkStateHandler extension)

> **Note:** No automated test suite. This project builds/runs on Windows. Each task ends with manual verification steps. The panel HTML can be tested in a regular browser (SimVar calls will return 0/null — that's fine for layout testing).

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `msfs-addon/fstrak-moving-map/manifest.json` | Create | MSFS addon metadata |
| `msfs-addon/fstrak-moving-map/layout.json` | Create (generated) | MSFS file index |
| `msfs-addon/fstrak-moving-map/html_ui/panel/index.html` | Create | Full panel: map, HUD, ATC, SimVar polling |
| `msfs-addon/generate_layout.py` | Create | Regenerates layout.json before release |
| `msfs-addon/README.md` | Create | User installation guide |
| `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs` | Modify | Add `airports` array to /network/state |
| `README.md` | Modify | Add Moving Map Tablet section |

---

### Task 1: Extend `/network/state` to include controlled airports

**Files:**
- Modify: `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs`

The existing `BuildResponse` method builds `firs` and `network`. We add an `airports` array sourced from `VatsimControlledAirports` (VATSIM) and non-CTR `IvaoAtcList` entries (IVAO).

- [ ] **Step 1: Add `BuildAirports` method to `NetworkStateHandler.cs`**

In `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs`, add the following private static method after `BuildPolygonFeature`:

```csharp
private static JArray BuildAirports(LiveViewViewModel lvm)
{
    var airports = new JArray();

    // VATSIM controlled airports
    if (lvm.IsVatsimActive && lvm.IsShowVatsimAtc)
    {
        foreach (var airport in lvm.VatsimControlledAirports)
        {
            var controllers = new JArray();
            foreach (var c in airport.Controllers)
            {
                controllers.Add(new JObject
                {
                    ["callsign"] = c.callsign,
                    ["frequency"] = c.frequency,
                    ["type"] = MapFacilityType(c.facility)
                });
            }

            // ATIS: join all text_atis lines from all Atis entries
            string atisText = null;
            if (airport.Atis != null)
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var a in airport.Atis)
                {
                    if (a.text_atis != null)
                        lines.AddRange(a.text_atis);
                }
                if (lines.Count > 0)
                    atisText = string.Join("\n", lines);
            }

            JArray polygon = null;
            int? radius = null;
            if (airport.IsShowTraconPolygon && airport.TraconPolygons.Count > 0)
            {
                polygon = new JArray();
                foreach (var loc in airport.TraconPolygons[0])
                    polygon.Add(new JArray(loc.Longitude, loc.Latitude));
            }
            else
            {
                radius = 25;
            }

            var entry = new JObject
            {
                ["callsign"] = airport.Callsign,
                ["lat"] = airport.Airport.Latitude,
                ["lon"] = airport.Airport.Longitude,
                ["controllers"] = controllers,
                ["atis"] = atisText
            };
            if (polygon != null) entry["polygon"] = polygon;
            if (radius != null) entry["radius"] = radius;

            airports.Add(entry);
        }
    }

    // IVAO airport-type entries (non-CTR)
    if (lvm.IsIvaoActive && lvm.IsShowIvaoAtc)
    {
        foreach (var atc in lvm.IvaoAtcList)
        {
            if (atc.IsCtr) continue; // CTR entries already in firs

            var controllers = new JArray();
            if (atc.AtcEntries != null)
            {
                foreach (var e in atc.AtcEntries)
                {
                    controllers.Add(new JObject
                    {
                        ["callsign"] = e.callsign,
                        ["frequency"] = e.atcSession?.frequency.ToString("F3") ?? "",
                        ["type"] = e.atcSession?.position ?? ""
                    });
                }
            }

            JArray polygon = null;
            int? radius = null;
            if (atc.ControlPolygon != null && atc.ControlPolygon.Count >= 3)
            {
                polygon = new JArray();
                foreach (var loc in atc.ControlPolygon)
                    polygon.Add(new JArray(loc.Longitude, loc.Latitude));
            }
            else
            {
                radius = 25;
            }

            var entry = new JObject
            {
                ["callsign"] = atc.Callsign,
                ["lat"] = atc.Location.Latitude,
                ["lon"] = atc.Location.Longitude,
                ["controllers"] = controllers,
                ["atis"] = (string)null
            };
            if (polygon != null) entry["polygon"] = polygon;
            if (radius != null) entry["radius"] = radius;

            airports.Add(entry);
        }
    }

    return airports;
}

private static string MapFacilityType(int facility)
{
    // VATSIM facility types: 0=OBS, 1=FSS, 2=DEL, 3=GND, 4=TWR, 5=APP, 6=CTR
    switch (facility)
    {
        case 1: return "FSS";
        case 2: return "DEL";
        case 3: return "GND";
        case 4: return "TWR";
        case 5: return "APP";
        case 6: return "CTR";
        default: return "OBS";
    }
}
```

- [ ] **Step 2: Wire `BuildAirports` into `BuildResponse`**

In `NetworkStateHandler.cs`, find `BuildResponse` and replace:

```csharp
return new JObject
{
    ["atcVisible"] = lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc,
    ["network"] = network,
    ["firs"] = features
};
```

With:

```csharp
return new JObject
{
    ["atcVisible"] = lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc,
    ["network"] = network,
    ["firs"] = features,
    ["airports"] = BuildAirports(lvm)
};
```

- [ ] **Step 3: Update `BuildEmptyResponse` to include `airports`**

Replace:

```csharp
private static JObject BuildEmptyResponse() =>
    new JObject
    {
        ["atcVisible"] = false,
        ["network"] = "none",
        ["firs"] = new JArray()
    };
```

With:

```csharp
private static JObject BuildEmptyResponse() =>
    new JObject
    {
        ["atcVisible"] = false,
        ["network"] = "none",
        ["firs"] = new JArray(),
        ["airports"] = new JArray()
    };
```

- [ ] **Step 4: Build and verify**

Build `Debug|x64` in Visual Studio. Confirm zero errors.

Manual check: enable VATSIM in FSTRaK live view with some ATC online. Open `http://localhost:8765/network/state` in a browser. Confirm JSON response includes `"airports": [...]` with at least one entry containing `callsign`, `lat`, `lon`, `controllers`, `atis`, and either `polygon` or `radius`.

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs
git commit -m "feat: add airports array to /network/state endpoint"
```

---

### Task 2: MSFS addon package skeleton

**Files:**
- Create: `msfs-addon/fstrak-moving-map/manifest.json`
- Create: `msfs-addon/generate_layout.py`

- [ ] **Step 1: Create `manifest.json`**

Create `msfs-addon/fstrak-moving-map/manifest.json`:

```json
{
  "dependencies": [],
  "content_type": "PANEL",
  "title": "FSTrAk Moving Map",
  "manufacturer": "FSTrAk",
  "creator": "FSTrAk",
  "package_version": "1.0.0",
  "minimum_game_version": "1.0.0",
  "release_notes": {
    "neutral": {
      "LastUpdate": "",
      "OlderHistory": ""
    }
  }
}
```

- [ ] **Step 2: Create `generate_layout.py`**

Create `msfs-addon/generate_layout.py`:

```python
import os
import json

addon_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fstrak-moving-map")
content = []

for root, dirs, files in os.walk(addon_dir):
    for fname in files:
        if fname == "layout.json":
            continue
        fpath = os.path.join(root, fname)
        rel = os.path.relpath(fpath, addon_dir).replace("\\", "/")
        # Convert Unix timestamp to Windows FILETIME (100-nanosecond intervals since 1601-01-01)
        mtime_filetime = int(os.path.getmtime(fpath) * 10000000) + 116444736000000000
        content.append({
            "path": rel,
            "size": os.path.getsize(fpath),
            "date": mtime_filetime
        })

layout = {"content": content}
layout_path = os.path.join(addon_dir, "layout.json")
with open(layout_path, "w") as f:
    json.dump(layout, f, indent=2)
print(f"layout.json written with {len(content)} entries.")
```

- [ ] **Step 3: Commit**

```bash
git add msfs-addon/fstrak-moving-map/manifest.json msfs-addon/generate_layout.py
git commit -m "feat: add MSFS addon manifest and layout generator"
```

---

### Task 3: Panel HTML — map, HUD, and aircraft marker

**Files:**
- Create: `msfs-addon/fstrak-moving-map/html_ui/panel/index.html`

This is the entire panel. We build it in three steps: structure + map + aircraft marker first, then ATC layer, then HUD/buttons.

- [ ] **Step 1: Download Leaflet 1.9.4 minified source**

Fetch Leaflet from the CDN and save inline. The file will be embedded directly in the HTML. Get the minified JS from:
`https://unpkg.com/leaflet@1.9.4/dist/leaflet.js`
and the CSS from:
`https://unpkg.com/leaflet@1.9.4/dist/leaflet.css`

You will inline both directly into `index.html` in the next step.

- [ ] **Step 2: Create `index.html` with map + aircraft marker**

Create `msfs-addon/fstrak-moving-map/html_ui/panel/index.html`. This file must be fully self-contained — no external URLs at runtime. Leaflet CSS and JS are inlined.

The transparent PNG data URI used for errorTileUrl:
`data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==`

```html
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8"/>
<title>FSTrAk Moving Map</title>
<style>
/* === LEAFLET CSS INLINE HERE === */
/* Paste full content of leaflet.css here */

* { margin: 0; padding: 0; box-sizing: border-box; }
html, body { width: 100%; height: 100%; background: #1a1a2e; overflow: hidden; }
#map { width: 100%; height: 100%; }

#hud {
  position: absolute; top: 0; left: 0; right: 0; z-index: 1000;
  background: rgba(0,0,0,0.65); color: #fff;
  font-family: monospace; font-size: 13px;
  padding: 6px 12px; display: flex; gap: 24px;
  pointer-events: none;
}
#hud span { white-space: nowrap; }

#btn-recenter {
  position: absolute; bottom: 40px; right: 10px; z-index: 1000;
  width: 36px; height: 36px; border-radius: 4px; border: none; cursor: pointer;
  background: rgba(0,0,0,0.7); color: #fff; font-size: 18px;
  display: none; align-items: center; justify-content: center;
}
#btn-recenter.visible { display: flex; }

#btn-atc {
  position: absolute; bottom: 84px; right: 10px; z-index: 1000;
  width: 36px; height: 36px; border-radius: 4px; border: none; cursor: pointer;
  background: rgba(0,0,0,0.7); color: #4fc3f7; font-size: 16px;
  display: flex; align-items: center; justify-content: center;
}
#btn-atc.atc-off { color: #666; }

.aircraft-icon {
  display: flex; align-items: center; justify-content: center;
}
.aircraft-icon svg {
  transform-origin: center center;
}
</style>
</head>
<body>

<div id="hud">
  <span id="hud-alt">ALT: --- ft</span>
  <span id="hud-spd">GS: --- kt</span>
  <span id="hud-hdg">HDG: ---°</span>
</div>

<div id="map"></div>

<button id="btn-recenter" title="Center on aircraft">✈</button>
<button id="btn-atc" title="Toggle ATC">◉</button>

<script>
/* === LEAFLET JS INLINE HERE === */
/* Paste full content of leaflet.js here */
</script>
<script>
// ── Constants ──────────────────────────────────────────────────────────────
const TILE_BASE    = 'http://localhost:8765/tiles/base/{z}/{x}/{y}';
const TILE_CHART   = 'http://localhost:8765/tiles/overlay/chart/{z}/{x}/{y}';
const TILE_OPENAIP = 'http://localhost:8765/tiles/overlay/openaip/{z}/{x}/{y}';
const STATE_URL    = 'http://localhost:8765/network/state';
const TOGGLE_URL   = 'http://localhost:8765/network/atc/toggle';
const TRANSPARENT  = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';

// ── State ──────────────────────────────────────────────────────────────────
let userPanned = false;
let atcVisible = true;
let lastLat = 0, lastLon = 0, lastHdg = 0;

// ── Map init ───────────────────────────────────────────────────────────────
const map = L.map('map', { zoomControl: true, attributionControl: false }).setView([0, 0], 10);

L.tileLayer(TILE_BASE, { maxZoom: 18, minZoom: 3 }).addTo(map);
L.tileLayer(TILE_CHART,   { maxZoom: 18, minZoom: 3, opacity: 0.8, errorTileUrl: TRANSPARENT }).addTo(map);
L.tileLayer(TILE_OPENAIP, { maxZoom: 18, minZoom: 3, opacity: 0.8, errorTileUrl: TRANSPARENT }).addTo(map);

// ── Aircraft marker ────────────────────────────────────────────────────────
function makeAircraftIcon(hdg) {
  return L.divIcon({
    className: 'aircraft-icon',
    html: `<svg width="28" height="28" viewBox="0 0 28 28" style="transform:rotate(${hdg}deg)" xmlns="http://www.w3.org/2000/svg">
      <path d="M14 2 L17 18 L14 16 L11 18 Z" fill="#4fc3f7" stroke="#fff" stroke-width="1"/>
      <path d="M7 12 L14 10 L21 12 L21 14 L14 13 L7 14 Z" fill="#4fc3f7" stroke="#fff" stroke-width="0.5"/>
      <path d="M10 19 L14 18 L18 19 L18 20 L14 19.5 L10 20 Z" fill="#4fc3f7" stroke="#fff" stroke-width="0.5"/>
    </svg>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14]
  });
}

const aircraftMarker = L.marker([0, 0], {
  icon: makeAircraftIcon(0),
  zIndexOffset: 1000
}).addTo(map);

// ── Pan/zoom tracking ──────────────────────────────────────────────────────
map.on('dragstart zoomstart', () => {
  userPanned = true;
  document.getElementById('btn-recenter').classList.add('visible');
});

document.getElementById('btn-recenter').addEventListener('click', () => {
  map.setView([lastLat, lastLon], map.getZoom());
  userPanned = false;
  document.getElementById('btn-recenter').classList.remove('visible');
});

// ── SimVar polling ─────────────────────────────────────────────────────────
function getSimVar(name, unit) {
  if (typeof SimVar !== 'undefined') {
    try { return SimVar.GetSimVarValue(name, unit); } catch(e) {}
  }
  return 0;
}

function updateAircraft() {
  const lat = getSimVar('PLANE LATITUDE', 'degrees');
  const lon = getSimVar('PLANE LONGITUDE', 'degrees');
  const hdg = getSimVar('PLANE HEADING DEGREES MAGNETIC', 'degrees');
  const alt = Math.round(getSimVar('INDICATED ALTITUDE', 'feet'));
  const spd = Math.round(getSimVar('AIRSPEED INDICATED', 'knots'));

  lastLat = lat; lastLon = lon; lastHdg = hdg;

  aircraftMarker.setLatLng([lat, lon]);
  aircraftMarker.setIcon(makeAircraftIcon(hdg));

  document.getElementById('hud-alt').textContent = `ALT: ${alt} ft`;
  document.getElementById('hud-spd').textContent = `GS: ${spd} kt`;
  document.getElementById('hud-hdg').textContent = `HDG: ${Math.round(hdg)}°`;

  if (!userPanned) {
    map.setView([lat, lon], map.getZoom(), { animate: false });
  }
}

setInterval(updateAircraft, 1000);
updateAircraft();

// ── ATC layer (loaded in Task 4) ───────────────────────────────────────────
// ATC fetch and toggle wired in next task
</script>
</body>
</html>
```

**Important:** In the actual file, replace the two comments `/* === LEAFLET CSS INLINE HERE === */` and `/* === LEAFLET JS INLINE HERE === */` with the actual content of `leaflet.css` and `leaflet.js` fetched from unpkg. The WebFetch tool can retrieve these.

- [ ] **Step 3: Test in browser**

Open `index.html` in any browser (double-click the file). Confirm:
- Map renders with OpenStreetMap tiles (if FSTRaK tile server is running with default OSM)
- No JS errors in the browser console
- HUD shows `ALT: 0 ft  GS: 0 kt  HDG: 0°` (SimVar returns 0 in browser — correct)
- ✈ button is hidden (not panned)
- Dragging the map shows the ✈ button; clicking it re-centers on [0,0]

- [ ] **Step 4: Commit**

```bash
git add "msfs-addon/fstrak-moving-map/html_ui/panel/index.html"
git commit -m "feat: add moving map panel with aircraft marker and SimVar polling"
```

---

### Task 4: ATC layer — FIRs, airports, toggle

**Files:**
- Modify: `msfs-addon/fstrak-moving-map/html_ui/panel/index.html`

Replace the `// ATC fetch and toggle wired in next task` comment with the full ATC layer implementation.

- [ ] **Step 1: Add ATC layer code**

In `index.html`, replace the comment `// ATC layer (loaded in Task 4)` and everything after it (up to the closing `</script>`) with:

```javascript
// ── ATC layer ──────────────────────────────────────────────────────────────
let atcLayer = L.layerGroup().addTo(map);

function buildPopupContent(airport) {
  let html = `<div style="font-family:monospace;font-size:12px;min-width:180px">`;
  html += `<b style="font-size:13px">${airport.callsign}</b><br>`;
  if (airport.controllers && airport.controllers.length > 0) {
    html += `<table style="width:100%;margin-top:4px;border-collapse:collapse">`;
    for (const c of airport.controllers) {
      html += `<tr><td>${c.callsign}</td><td style="text-align:right;padding-left:8px">${c.frequency}</td><td style="text-align:right;padding-left:8px;color:#888">${c.type}</td></tr>`;
    }
    html += `</table>`;
  }
  if (airport.atis) {
    html += `<hr style="margin:6px 0;border-color:#444">`;
    html += `<div style="color:#aaa;font-size:11px;white-space:pre-wrap">${airport.atis}</div>`;
  }
  html += `</div>`;
  return html;
}

function renderAtcState(state) {
  atcLayer.clearLayers();

  atcVisible = state.atcVisible;
  const btn = document.getElementById('btn-atc');
  btn.classList.toggle('atc-off', !atcVisible);

  if (!state.atcVisible) return;

  // FIR polygons
  if (state.firs) {
    for (const feature of state.firs) {
      if (!feature.geometry || !feature.geometry.coordinates) continue;
      const coords = feature.geometry.coordinates[0].map(c => [c[1], c[0]]);
      if (coords.length < 3) continue;
      L.polygon(coords, {
        color: '#4fc3f7',
        weight: 1,
        fillColor: '#4fc3f7',
        fillOpacity: 0.08
      }).bindTooltip(feature.properties.callsign || '', { sticky: true }).addTo(atcLayer);
    }
  }

  // Airport markers, polygons/circles
  if (state.airports) {
    for (const airport of state.airports) {
      // Dot marker
      const dotIcon = L.divIcon({
        className: '',
        html: `<svg width="10" height="10" viewBox="0 0 10 10" xmlns="http://www.w3.org/2000/svg">
          <circle cx="5" cy="5" r="4" fill="#ff9800" stroke="#fff" stroke-width="1"/>
        </svg>`,
        iconSize: [10, 10],
        iconAnchor: [5, 5]
      });

      L.marker([airport.lat, airport.lon], { icon: dotIcon, zIndexOffset: 500 })
        .bindPopup(buildPopupContent(airport), { maxWidth: 280 })
        .addTo(atcLayer);

      // TRACON polygon or circle
      if (airport.polygon && airport.polygon.length >= 3) {
        const coords = airport.polygon.map(c => [c[1], c[0]]);
        L.polygon(coords, {
          color: '#ff9800',
          weight: 1,
          fillColor: '#ff9800',
          fillOpacity: 0.08
        }).addTo(atcLayer);
      } else if (airport.radius) {
        L.circle([airport.lat, airport.lon], {
          radius: airport.radius * 1852, // nm to metres
          color: '#ff9800',
          weight: 1,
          fillColor: '#ff9800',
          fillOpacity: 0.06
        }).addTo(atcLayer);
      }
    }
  }
}

async function fetchAtcState() {
  try {
    const resp = await fetch(STATE_URL);
    if (!resp.ok) return;
    const state = await resp.json();
    renderAtcState(state);
  } catch (e) {
    // tile server not running — silently skip
  }
}

document.getElementById('btn-atc').addEventListener('click', async () => {
  try {
    const resp = await fetch(TOGGLE_URL, { method: 'POST' });
    if (!resp.ok) return;
  } catch (e) {}
  await fetchAtcState();
});

// Initial fetch and periodic refresh
fetchAtcState();
setInterval(fetchAtcState, 30000);
```

- [ ] **Step 2: Test ATC layer in browser**

With FSTRaK running and VATSIM active with some ATC online:
1. Open `index.html` in a browser
2. Confirm FIR polygons appear as light blue shaded areas
3. Confirm airport dots appear in orange
4. Click an airport dot — confirm popup shows callsign, controllers list, and ATIS if available
5. Click ◉ button — confirm polygons disappear and button turns grey; click again — confirm they return

- [ ] **Step 3: Commit**

```bash
git add "msfs-addon/fstrak-moving-map/html_ui/panel/index.html"
git commit -m "feat: add ATC layer (FIRs, airports, polygons/circles, popups, toggle)"
```

---

### Task 5: Generate `layout.json` and verify addon structure

**Files:**
- Create: `msfs-addon/fstrak-moving-map/layout.json` (generated)

- [ ] **Step 1: Run the layout generator**

From the `msfs-addon/` directory:

```bash
cd msfs-addon
python generate_layout.py
```

Expected output:
```
layout.json written with 2 entries.
```
(2 entries: `manifest.json` and `html_ui/panel/index.html`)

- [ ] **Step 2: Verify `layout.json` content**

Open `msfs-addon/fstrak-moving-map/layout.json`. It should look like:

```json
{
  "content": [
    {
      "path": "html_ui/panel/index.html",
      "size": 123456,
      "date": 133500000000000000
    },
    {
      "path": "manifest.json",
      "size": 245,
      "date": 133500000000000000
    }
  ]
}
```

Exact sizes and dates will differ — that's fine.

- [ ] **Step 3: Commit**

```bash
git add msfs-addon/fstrak-moving-map/layout.json
git commit -m "chore: generate layout.json for MSFS addon"
```

---

### Task 6: Installation guide and README update

**Files:**
- Create: `msfs-addon/README.md`
- Modify: `README.md`

- [ ] **Step 1: Create `msfs-addon/README.md`**

Create `msfs-addon/README.md`:

```markdown
# FSTrAk Moving Map — MSFS 2024 Tablet Panel

A moving map panel for Microsoft Flight Simulator 2024 that shows your current map selection from FSTrAk, including aviation chart overlays, live ATC coverage, and your aircraft position.

## Prerequisites

- FSTrAk v3.5.1 or later installed and running
- Microsoft Flight Simulator 2024
- Python 3 (only needed if you modify the panel and need to regenerate `layout.json`)

## Installation

1. **Enable the tile server in FSTrAk**
   Open FSTrAk → Settings → check **Enable Tile Server**. The status line should show `● Running on http://localhost:8765/`.

2. **Copy the addon to your Community folder**
   Copy the entire `fstrak-moving-map/` folder into your MSFS Community folder.
   Default locations:
   - Microsoft Store: `%LocalAppData%\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Community`
   - Steam: `%AppData%\Microsoft Flight Simulator\Packages\Community`

3. **Start MSFS 2024**
   The addon will appear in the Content Manager as **FSTrAk Moving Map**.

4. **Open the panel in-sim**
   The panel is available as a toolbar item. Click the FSTrAk icon in the instrument toolbar to open the moving map.

## Usage

- **Map follows your aircraft** by default. Pan or zoom to explore freely.
- **✈ button** (bottom-right): re-centers the map on your aircraft after panning.
- **◉ button** (above ✈): toggles ATC overlay on/off. This also toggles ATC visibility in FSTrAk's live view.
- **ATC polygons** update every 30 seconds from FSTrAk.
- **Click an airport dot** to see active controllers, frequencies, and ATIS (VATSIM only).

## Verify the tile server is working

Open a browser and navigate to:
```
http://localhost:8765/tiles/base/5/20/13
```
You should see a map tile image. If you get an error, make sure FSTrAk is running with the tile server enabled.

## Regenerating `layout.json` (developers only)

If you modify `index.html` or `manifest.json`, regenerate the layout file before testing in MSFS:

```bash
cd msfs-addon
python generate_layout.py
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Blank map in panel | Check FSTrAk is running and tile server is enabled in Settings |
| No ATC polygons | Enable VATSIM or IVAO in FSTrAk Live View |
| Panel not appearing in MSFS | Verify `fstrak-moving-map/` is in the Community folder and MSFS was restarted |
| Wrong map style | Change map provider in FSTrAk Settings — the panel mirrors your selection |
```

- [ ] **Step 2: Update `README.md`**

In the repo root `README.md`, find the `## Features` section. Add a new bullet after the existing live map bullet:

```markdown
* **MSFS 2024 in-sim moving map** — an MSFS Community addon panel that mirrors your FSTrAk map selection, showing chart overlays, live ATC coverage, and your aircraft position inside the simulator.
```

Then find the `## Roadmap` section and add:

```markdown
- [x] MSFS 2024 in-sim moving map tablet panel.
```

- [ ] **Step 3: Build and verify**

Build `Debug|x64` in Visual Studio — confirm zero errors (only README changes, no C# changes in this task).

- [ ] **Step 4: Commit**

```bash
git add msfs-addon/README.md README.md
git commit -m "docs: add MSFS tablet panel install guide and update README"
```

---

### Task 7: Manual end-to-end verification

No code changes — verification only.

- [ ] **Step 1: Verify `/network/state` airports**

With FSTRaK running and VATSIM active:
```
curl http://localhost:8765/network/state
```
Confirm response has `"airports": [...]` with entries. If no VATSIM ATC online, airports array will be empty — that's correct.

- [ ] **Step 2: Verify addon structure**

Confirm this folder tree exists:
```
msfs-addon/
├── README.md
├── generate_layout.py
└── fstrak-moving-map/
    ├── manifest.json
    ├── layout.json
    └── html_ui/
        └── panel/
            └── index.html
```

- [ ] **Step 3: Test panel in browser**

Open `msfs-addon/fstrak-moving-map/html_ui/panel/index.html` in a browser with FSTrAk tile server running:
- Base map tiles load
- Chart overlay tiles load (or render transparent if no overlay selected)
- ATC polygons render if VATSIM/IVAO active
- Airport dots clickable with popup
- ◉ toggle works
- ✈ recenter button appears on pan, works on click

- [ ] **Step 4: Test in MSFS 2024 (if available)**

Copy `fstrak-moving-map/` to MSFS Community folder. Start MSFS. Confirm addon loads (check Content Manager). Open panel in toolbar. Confirm aircraft marker moves with heading, map updates.

- [ ] **Step 5: Final commit if any fixes were needed**

```bash
git add -p
git commit -m "fix: tablet panel verification corrections"
```
