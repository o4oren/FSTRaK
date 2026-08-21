# FSTRaK 3.7.0 Release Notes

## 3.7.0

### SimBrief Integration
- New setting: SimBrief username / Pilot ID (Settings → leave empty to disable).
- When a flight starts, FSTRaK silently fetches your latest SimBrief OFP and matches it against the detected departure airport (re-checked at taxi-out, which takes precedence).
- A "Plan" toggle on the live map overlays the planned route with waypoint labels and planned altitude/speed/fuel tooltips.
- When you land at the planned arrival or a planned alternate, the plan (aircraft, airports, route, fuel, times, weights, passengers/cargo and all navlog points) is saved with the flight.
- Flight details show the planned route overlay and a planned-vs-actual card (fuel, block time, distance, payload, pax/cargo), including a DIVERTED indicator when you landed at an alternate.
- The logbook shows the planned flight number (e.g. BAW0414); a blank aircraft airline is backfilled from the plan.

## 3.6.2

### New Features

**Statistics Chart Zoom**
The bar charts in the statistics view (Flights per period and Landing Rate Distribution) can now be zoomed with the mouse wheel and panned by dragging, so you can inspect a smaller timespan at greater resolution.

### Improvements

- The landing line in the flight details scoreboard now shows the touchdown vertical speed and G force, making it clear when the G reading drove the landing rating (e.g. "Hard Landing (-210 fpm, 1.62 G) -35 Points").
- The log now records the G force at the touchdown instant as well as the peak G within the two-second sampling window.
- Security updates to the EFB app's build dependencies.

### Bug Fixes

- The "Flights per" statistics chart now shows a continuous timeline on a true date axis — days, months, or years without flights appear as gaps instead of being silently compressed out.
- The landing-rate histogram and the average landing V/S stat no longer include flights that never recorded a landing.
- Chart axis labels no longer keep stale colors after switching theme and chart period.
- EFB app: fixed packaging metadata (layout.json) that recorded wrong file sizes for the app icon and manifest.
