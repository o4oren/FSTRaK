using FSTRaK.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MapControl;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /network/state
    /// Returns current ATC visibility, active network, and FIR/UIR polygons as GeoJSON features.
    /// </summary>
    internal class NetworkStateHandler
    {
        public async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var lvm = App.LiveViewViewModel;

                JObject response;

                if (lvm == null)
                {
                    response = BuildEmptyResponse();
                }
                else
                {
                    response = await Application.Current.Dispatcher.InvokeAsync(() => BuildResponse(lvm));
                }

                var json = response.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NetworkStateHandler: error building response");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.OutputStream.Close();
            }

        }

        private static JObject BuildResponse(LiveViewViewModel lvm)
        {
            var features = new JArray();

            // VATSIM FIRs
            if (lvm.IsVatsimActive && lvm.IsShowVatsimAtc)
            {
                foreach (var fir in lvm.VatsimControlledFirs)
                {
                    foreach (var locations in fir.Locations ?? new System.Collections.Generic.List<LocationCollection>())
                    {
                        var feature = BuildPolygonFeature(locations, fir.Callsign, GetFirstFrequency(fir.Controllers));
                        if (feature != null) features.Add(feature);
                    }
                }

                foreach (var uir in lvm.VatsimControlledUirs)
                {
                    foreach (var locations in uir.FirLocations ?? new System.Collections.Generic.List<LocationCollection>())
                    {
                        var feature = BuildPolygonFeature(locations, uir.Callsign, GetFirstFrequency(uir.Controllers));
                        if (feature != null) features.Add(feature);
                    }
                }
            }

            // IVAO CTR polygons
            if (lvm.IsIvaoActive && lvm.IsShowIvaoAtc)
            {
                foreach (var atc in lvm.IvaoAtcList)
                {
                    if (atc.ControlPolygon != null && atc.ControlPolygon.Count > 0)
                    {
                        var feature = BuildPolygonFeature(atc.ControlPolygon, atc.Callsign, null);
                        if (feature != null) features.Add(feature);
                    }
                }
            }

            string network = "none";
            if (lvm.IsVatsimActive) network = "vatsim";
            else if (lvm.IsIvaoActive) network = "ivao";

            Log.Debug("NetworkStateHandler: atcVisible={AtcVisible} network={Network} firs={FirCount} vatsimFirs={VatsimFirCount} uirs={UirCount} isVatsimActive={IsVatsimActive} isShowVatsimAtc={IsShowVatsimAtc}",
                lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc, network, features.Count,
                lvm.VatsimControlledFirs.Count, lvm.VatsimControlledUirs.Count,
                lvm.IsVatsimActive, lvm.IsShowVatsimAtc);

            return new JObject
            {
                ["atcVisible"] = lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc,
                ["network"] = network,
                ["firs"] = features,
                ["airports"] = BuildAirports(lvm)
            };
        }

        private static JObject BuildPolygonFeature(LocationCollection locations, string callsign, string frequency)
        {
            if (locations == null || locations.Count < 3) return null;

            var ring = new JArray();
            foreach (var loc in locations)
                ring.Add(new JArray(loc.Longitude, loc.Latitude));

            // Close the ring if not already closed
            var firstLoc = locations[0];
            var lastLoc = locations[locations.Count - 1];
            if (firstLoc.Latitude != lastLoc.Latitude || firstLoc.Longitude != lastLoc.Longitude)
                ring.Add(new JArray(firstLoc.Longitude, firstLoc.Latitude));

            var props = new JObject { ["callsign"] = callsign };
            if (frequency != null) props["frequency"] = frequency;

            return new JObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JObject
                {
                    ["type"] = "Polygon",
                    ["coordinates"] = new JArray(ring)
                },
                ["properties"] = props
            };
        }

        private static string GetFirstFrequency(IEnumerable<FSTRaK.BusinessLogic.VatsimService.VatsimModel.Controller> controllers)
        {
            foreach (var c in controllers)
                return c.frequency;
            return null;
        }

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
                    if (atc.IsCtr) continue;

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

        private static JObject BuildEmptyResponse() =>
            new JObject
            {
                ["atcVisible"] = false,
                ["network"] = "none",
                ["firs"] = new JArray(),
                ["airports"] = new JArray()
            };
    }
}
