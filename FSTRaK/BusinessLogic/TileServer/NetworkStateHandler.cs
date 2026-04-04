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
        public Task HandleAsync(HttpListenerContext context)
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
                    response = Application.Current.Dispatcher.Invoke(() => BuildResponse(lvm));
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

            return Task.CompletedTask;
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

            return new JObject
            {
                ["atcVisible"] = lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc,
                ["network"] = network,
                ["firs"] = features
            };
        }

        private static JObject BuildPolygonFeature(LocationCollection locations, string callsign, string frequency)
        {
            if (locations == null || locations.Count < 3) return null;

            var ring = new JArray();
            foreach (var loc in locations)
                ring.Add(new JArray(loc.Longitude, loc.Latitude));

            // Close the ring if not already closed
            if (locations.Count > 0)
            {
                var first = locations[0];
                ring.Add(new JArray(first.Longitude, first.Latitude));
            }

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

        private static JObject BuildEmptyResponse() =>
            new JObject
            {
                ["atcVisible"] = false,
                ["network"] = "none",
                ["firs"] = new JArray()
            };
    }
}
