# FSTRaK 3.7.5 Release Notes

This is a stability release focused on how FSTRaK handles losing its connection to the simulator.

## Fixes

**Connection loss no longer leaves FSTRaK in a broken state**
- A SimConnect pipe error previously left FSTRaK polling a dead connection indefinitely, appearing connected while receiving nothing. All connection errors now tear down cleanly and reconnect.
- A dropped connection mid-flight now holds the flight for 60 seconds instead of abandoning it. If the simulator comes back within that window with the same aircraft near your last known position, the flight simply continues — a brief connection blip no longer costs you a long flight.
- If the reconnected session is a different flight (different aircraft, or a jump to another airport), the original flight is ended rather than silently continued.
- Closing MSFS mid-flight previously left the flight hanging indefinitely, with FSTRaK still showing it as active. The flight now ends cleanly.

  Note: a flight ended this way is still not written to the logbook — only flights that reach a parking spot are saved. Recovering an interrupted flight into the logbook is a separate change, not part of this release.

**Leaving a flight while paused is now detected reliably**
- Camera state — which is how FSTRaK detects a flight starting and ending — is now polled independently of flight data, so pausing or opening a menu no longer blinds the detection.
- Pausing immediately after landing and then quitting now finalises the landing correctly, including its touchdown G force. Previously the landing could be dropped.

**Other**
- Fixed a connection-setup failure that could leave FSTRaK permanently unable to reconnect until restarted.
- Fixed aircraft records created immediately after a reconnect being saved with incorrect livery handling on MSFS 2020.

## Changes

- Flight data now arrives on a SimConnect subscription tied to the simulator's physics loop, instead of being polled 20 times a second. Landing detection sees every sample, which slightly improves the precision of landing scores. The displayed flight data and live map continue to refresh 20 times a second, which is what they did before and is well past the point of visible difference.
- Because samples now follow the simulator's physics loop, pausing within about two seconds of touchdown will end G-force sampling for that landing. The frozen simulation has nothing further to measure, so the resulting score reflects the actual touchdown.

## Under the hood

- All SimConnect calls are now synchronised, removing a race between the UI thread and the background polling timers.
- Flight-state detection and the reconnect identity check are now covered by unit tests.
