# FSTrAk Moving Map — MSFS 2024 Toolbar Panel

A moving map toolbar panel for Microsoft Flight Simulator 2024 that shows your current map selection from FSTrAk, including aviation chart overlays, live ATC coverage, and your aircraft position.

## Prerequisites

- FSTrAk v3.5.1 or later installed and running
- Microsoft Flight Simulator 2024

## Installation

1. **Enable the tile server in FSTrAk**
   Open FSTrAk → Settings → check **Enable Tile Server**. The status line should show `● Running on http://localhost:8765/`.

2. **Copy the addon to your Community folder**
   Copy the entire `fstrak-ingame-panel/` folder into your MSFS Community folder.
   Default locations:
   - Microsoft Store: `%LocalAppData%\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Community`
   - Steam: `%AppData%\Microsoft Flight Simulator\Packages\Community`

3. **Copy `FSTrAkMovingMap.html` to the FSTrAk folder**
   Copy `FSTrAkMovingMap.html` to the same folder where `FSTrAk.exe` is installed, inside a subfolder called `msfs-addon`:
   ```
   C:\Program Files\FSTrAk\msfs-addon\FSTrAkMovingMap.html
   ```

4. **Start MSFS 2024**
   The addon will appear in the Content Manager as **FSTrAk Moving Map**.

5. **Open the panel in-sim**
   Click the FSTrAk airplane icon in the instrument toolbar to open the moving map.

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

## For developers — rebuilding the SPB

The `fstrak-ingame-panel/InGamePanels/fstrak-ingame-panel.spb` file must be rebuilt using the MSFS SDK whenever the package name changes. To rebuild:

1. Install the MSFS SDK via Developer Mode in MSFS 2024 (Options → General → Developers → Enable Developer Mode → Help → SDK Installer)
2. Open a command prompt in the `msfs-addon/` directory
3. Run:
   ```
   build.bat
   ```
4. Run `python generate_layout.py` to update `layout.json`

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Blank map in panel | Check FSTrAk is running and tile server is enabled in Settings |
| No ATC polygons | Enable VATSIM or IVAO in FSTrAk Live View |
| Panel not appearing in MSFS | Verify `fstrak-ingame-panel/` is in the Community folder and MSFS was restarted |
| Wrong map style | Change map provider in FSTrAk Settings — the panel mirrors your selection |
