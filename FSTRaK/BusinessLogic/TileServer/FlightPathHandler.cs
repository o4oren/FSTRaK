using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /flight/path — returns the current active flight's recorded path as a JSON array
    /// of [lat, lon] pairs, ordered chronologically. Returns an empty array if no flight is active.
    /// </summary>
    internal class FlightPathHandler
    {
        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var fm = FlightManager.FlightManager.Instance;
                var flight = fm.ActiveFlight;

                JArray coords;
                if (flight != null && flight.FlightEvents != null && flight.FlightEvents.Count > 0)
                {
                    coords = new JArray(
                        flight.FlightEvents
                            .OrderBy(e => e.Id)
                            .Select(e => new JArray(e.Latitude, e.Longitude)));
                }
                else
                {
                    coords = new JArray();
                }

                var json = coords.ToString(Formatting.None);
                Log.Debug("FlightPathHandler: returning {Count} points", coords.Count);

                var bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FlightPathHandler error");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.OutputStream.Close();
            }

            return Task.CompletedTask;
        }
    }
}
