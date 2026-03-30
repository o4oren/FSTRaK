
# FSTRaK

![FSTrAk](https://github.com/o4oren/FSTRaK/assets/23486326/024542ad-ca3d-4e81-9fe8-7dec53ea6ab1)

FSTrAk is a modern flight tracker and logbook for MSFS (and possibly FSX/P3D, although not tested).
It aims to be a no frills, install and let it run experience - as it requires no manual intervention.
FSTrAk monitors your simulator silently, and will detect when a flight is started, track it on a map, and persist it to a local database when it is complete.

## Features
* Automatic silent start-up.
* Automatic flight tracking (hands-free experience).
* Option (default) to save only complete flights - i.e. flight ended in parking state, with engines off and parking brake set, after having flown.
* Multiple map providers including FAA ArcGIS charts, OpenAIP, SkyVector, OpenTopoMap, Bing, MapTiler, and more.
* Flight analysis and scoring.
* Live flight tracking with a moving map.
* **VATSIM and IVAO live network support** — view pilots and ATC on the live map.
* Statistics (most used aircraft, most used airlines, average and max payload, distance, etc.)
* Dark mode.

## API Keys

Some features require optional API keys, configured in the **Settings** view.

### StatSim API Key (VATSIM flight track history)

StatSim provides historical VATSIM flight tracks. When configured, clicking a VATSIM pilot on the live map shows their full flight path since departure instead of a simple geodesic line.

1. Go to [https://statsim.net/about](https://statsim.net/about)
2. Click **Login with VATSIM to get API Key** and authenticate with your VATSIM account
3. Create an API key in your account page
4. Paste the key into **Settings → StatSim API Key**

### IVAO API Key (IVAO flight track history & enriched details)

The IVAO API key enables full flight track history for IVAO pilots, plus enriched details for pilots and ATC (controller name, rating, ATIS, route, remarks) when clicking on them in the live map.

1. Go to [https://developer.ivao.aero](https://developer.ivao.aero) and log in with your IVAO account
2. Create a new application to obtain an API key
3. Paste the key into **Settings → IVAO API Key**

Without an API key, IVAO pilots show a geodesic line from departure to their current position, and ATC details are limited to what the public feed provides.

### Other API Keys

| Key | Feature | Where to get it |
|-----|---------|----------------|
| MapTiler | MapTiler map tiles | [https://cloud.maptiler.com](https://cloud.maptiler.com) — free tier available |
| OpenAIP | Aviation chart overlay | [https://www.openaip.net](https://www.openaip.net) — free registration |
| Azure Maps | Azure Maps tile layers | [https://portal.azure.com](https://portal.azure.com) — free tier available |

---

## Roadmap
- [ ] Simbrief integration (fetch passengers, planned vs actual fuel and time, planned vs actual route).
- [ ] More statistics.
- [ ] Display bearing/distance to a designated point on the map.
- [x] VATSIM integration (display live traffic and ATC on the map).
- [x] IVAO integration (display live traffic and ATC on the map).

#### Flight Analysis
<img width="1024" alt="Flight Analysis" src="https://github.com/o4oren/FSTRaK/assets/23486326/e6047bcc-df6a-4235-ac85-da6de6de8cd5">

#### Live Tracking, with OpenTopoMaps
<img width="1024" alt="OpenTopoMap" src="https://github.com/o4oren/FSTRaK/assets/23486326/7dfcfb9d-31e8-402c-914a-ff24d2760754">

#### Scoring events in flight analysis, with Bing Hybrid map
<img width="1024" alt="Scoring" src="https://github.com/o4oren/FSTRaK/assets/23486326/0242d88c-c1c1-4b58-8150-28da3c01f1d9">

#### SkyVector VFR map tiles
<img width="1024" alt="SkyVector" src="https://github.com/o4oren/FSTRaK/assets/23486326/54ae3f96-a6a8-4845-a5c6-94472a1826d4">

#### Analyzing route over SkyVector IFR map tiles
<img width="1024" alt="ifr" src="https://github.com/o4oren/FSTRaK/assets/23486326/3a44c195-8086-48ba-b9bb-2021fd6459f8">
