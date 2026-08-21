# FSTRaK 3.7.0 Release Notes

## New Features

**SimBrief Integration**
- New setting: SimBrief username / Pilot ID (Settings → leave empty to disable).
- When a flight starts, FSTRaK silently fetches your latest SimBrief OFP and matches it against the detected departure airport (re-checked at taxi-out, which takes precedence).
- A "Plan" toggle on the live map overlays the planned route with waypoint labels and planned altitude/speed/fuel tooltips.
- When you land at the planned arrival or a planned alternate, the plan (aircraft, airports, route, fuel, times, weights, passengers/cargo and all navlog points) is saved with the flight.
- Flight details show the planned route overlay and a planned-vs-actual card (fuel, block time, distance, payload, pax/cargo), including a DIVERTED indicator when you landed at an alternate.
- The logbook shows the planned flight number (e.g. BAW0414); a blank aircraft airline is backfilled from the plan.
