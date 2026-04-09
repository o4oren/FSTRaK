# FSTrAk MSFS Addons

This folder contains two MSFS Community addons for FSTrAk. Both are included in the release zip alongside the FSTrAk installer.

---

## 1. Toolbar Panel (`fstrak-ingame-panel`)

A moving map toolbar panel for MSFS 2020 and 2024. Appears as an airplane icon in the instrument toolbar.

### Prerequisites

- FSTrAk v3.7.0 or later installed and running
- Microsoft Flight Simulator 2020 or 2024

### Installation

1. Copy the `fstrak-ingame-panel/` folder into your MSFS **Community** folder:
   - **Microsoft Store / Xbox App (2024):** `%LocalAppData%\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Community`
   - **Microsoft Store / Xbox App (2020):** `%AppData%\Microsoft Flight Simulator\Packages\Community`
   - **Steam:** `%AppData%\Microsoft Flight Simulator\Packages\Community`
2. Restart MSFS.
3. The **FSTrAk Moving Map** icon appears in the in-sim toolbar.

### Usage

- Click the FSTrAk airplane icon in the toolbar to open the moving map.
- **Map follows your aircraft** by default. Pan or zoom to explore freely.
- **+ / −** (top-left): zoom in/out.
- **Crosshair button** (bottom-right): re-centers on your aircraft after panning.
- **ATC button** (above crosshair): toggles ATC overlay on/off.
- **ATC polygons** update every 30 seconds from FSTrAk.
- Hover over a FIR/UIR to see controller callsigns and frequencies.
- Click an airport dot to see active controllers, frequencies, and ATIS (VATSIM only).

---

## 2. EFB App (`fstrak-efb-app`)

The same moving map as a tablet (EFB) app, available on the MSFS 2024 tablet's home screen.

### Prerequisites

- FSTrAk v3.7.0 or later installed and running
- Microsoft Flight Simulator **2024** (EFB does not exist in MSFS 2020)

### Installation

1. Copy the `fstrak-efb-app/` folder into your MSFS 2024 **Community** folder:
   - **Microsoft Store / Xbox App:** `%LocalAppData%\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Community`
   - **Steam:** `%AppData%\Microsoft Flight Simulator\Packages\Community`
2. Restart MSFS 2024.
3. Open the in-sim tablet (EFB). The **FSTRaK** app appears on the home screen.

### Usage

- Tap the FSTRaK icon on the EFB home screen to open the moving map.
- All features are identical to the toolbar panel — moving map, ATC overlay, flight path, and HUD.
- FSTRaK must be running on the same PC for the map to work.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Blank map in toolbar panel or EFB app | Check FSTrAk is running and tile server is enabled in Settings |
| No ATC polygons | Enable VATSIM or IVAO in FSTrAk Live View |
| Toolbar panel not appearing | Verify `fstrak-ingame-panel/` is in the Community folder and MSFS was restarted |
| EFB app not appearing on tablet | Verify `fstrak-efb-app/` is in the Community folder and MSFS 2024 was restarted. EFB apps require MSFS 2024 — not available in MSFS 2020. |
| EFB app shows blank / "FSTRaK is not running" | FSTRaK desktop app is not running or tile server is disabled in Settings |
| Wrong map style | Change map provider in FSTrAk Settings — the panel mirrors your selection |

---

## Verify the tile server is working

Open a browser and navigate to:
```
http://localhost:8765/tiles/base/5/20/13
```
You should see a map tile image. If you get an error, make sure FSTrAk is running with the tile server enabled in Settings.

---

## For developers — rebuilding

### Toolbar panel SPB

The `fstrak-ingame-panel/InGamePanels/fstrak-ingame-panel.spb` must be rebuilt via the MSFS SDK whenever the package name changes:

1. Install the MSFS SDK via Developer Mode → Help → SDK Installer.
2. Open a command prompt in `msfs-addon/` and run:
   ```
   build.bat
   ```
3. Regenerate `layout.json`:
   ```
   python generate_layout.py
   ```

### EFB app JS bundle

The EFB app requires a build step (TypeScript → JavaScript via esbuild). Run this **once after cloning or after updating source files**:

1. **Copy `efb_api/` from the MSFS SDK:**
   The build depends on `@efb/efb-api` which is not committed to git (it ships with the MSFS 2024 SDK).
   - Install the MSFS 2024 SDK via Developer Mode → Help → SDK Installer.
   - Copy the `efb_api/` folder from `<SDK install path>\EFB\TemplateApp\efb_api\` into `msfs-addon/fstrak-efb-app/PackageSources/efb_api/`.

2. **Install npm dependencies:**
   ```
   cd msfs-addon/fstrak-efb-app/PackageSources/FSTRaKApp
   npm install
   ```

3. **Build the bundle:**
   ```
   npm run build
   ```
   Output lands in `PackageSources/FSTRaKApp/dist/`.

4. **Regenerate `layout.json`:**
   ```
   cd msfs-addon
   python generate_layout.py
   ```
   (The script auto-targets `fstrak-efb-app/`.)

5. During active development, use `npm run watch` to rebuild automatically on save.
