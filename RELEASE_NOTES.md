# FSTRaK 3.6.2 Release Notes

## New Features

**Statistics Chart Zoom**
The bar charts in the statistics view (Flights per period and Landing Rate Distribution) can now be zoomed with the mouse wheel and panned by dragging, so you can inspect a smaller timespan at greater resolution.

## Improvements

- The landing line in the flight details scoreboard now shows the touchdown vertical speed and G force, making it clear when the G reading drove the landing rating (e.g. "Hard Landing (-210 fpm, 1.62 G) -35 Points").
- The log now records the G force at the touchdown instant as well as the peak G within the two-second sampling window.
- Security updates to the EFB app's build dependencies.

## Bug Fixes

- The "Flights per" statistics chart now shows a continuous timeline on a true date axis — days, months, or years without flights appear as gaps instead of being silently compressed out.
- The landing-rate histogram and the average landing V/S stat no longer include flights that never recorded a landing.
- Chart axis labels no longer keep stale colors after switching theme and chart period.
- EFB app: fixed packaging metadata (layout.json) that recorded wrong file sizes for the app icon and manifest.
