# FSTRaK 3.6.1 Release Notes

## New Features

**Touchdown G Force**
The peak G force at touchdown is now captured and shown in the flight details and the landing event popup. Landings are scored on the worst of the vertical-speed (FPM) rating and the G-force rating, so a flat-but-hard slam no longer scores as a greaser. Older flights without G data simply show no G value.

**Edit Flight Airports**
A new **Edit Flight** option in the logbook context menu lets you correct a flight's departure and arrival airports — useful when the automatic nearest-airport detection picks a neighboring field. The editor suggests the airports closest to where the flight actually started and ended (with distances), and also accepts any airport ident code, validated against the airport database.

**Bounce and Settle-Back Protection**
Landing measurements are now protected against transient ground contacts:
- A brief settle-back onto the runway within 5 seconds of liftoff is no longer logged as a landing.
- Touchdowns within 5 seconds of each other are merged into a single landing event that keeps the worst vertical speed and the peak G force across all touchdowns, so a bounced landing is scored on its hardest impact instead of producing duplicate landing events.

**MSFS 2024 EFB App**
The FSTrAk moving map is now also available as an EFB app on the MSFS 2024 tablet home screen, in addition to the toolbar panel. Both addons are included in the release zip — copy `fstrak-efb-app/` (and/or `fstrak-ingame-panel/`) into your Community folder.

**Open Flightmaps Overlay**
Added an Open Flightmaps aeronautical chart overlay (European coverage).

## Improvements

- Fuel quantity is now recorded on every flight event and shown in event popups (events from older flights show no fuel data).

## Bug Fixes

- Touchdown G force now correctly reflects the peak over the two seconds following touchdown; previously only the touchdown instant was sampled.
- Bounced landings no longer create duplicate landing events or double score penalties.
