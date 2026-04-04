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
