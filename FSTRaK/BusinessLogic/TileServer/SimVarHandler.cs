using FSTRaK.BusinessLogic.FlightManager;
using Serilog;
using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /simvar — returns current aircraft position/state as JSON, read directly from
    /// FlightManager.CurrentFlightParams (sourced from SimConnect at 50 ms intervals).
    /// The MSFS toolbar panel iframe polls this endpoint; no SimVar access needed in the panel JS.
    /// </summary>
    internal class SimVarHandler
    {
        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var p = FlightManager.Instance.CurrentFlightParams;
                var json = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"lat\":{0},\"lon\":{1},\"hdg\":{2},\"alt\":{3},\"spd\":{4}}}",
                    p.Latitude, p.Longitude, p.Heading, p.Altitude, p.GroundSpeed);

                Log.Debug("SimVarHandler: GET — {Json}", json);

                var bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SimVarHandler error");
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
