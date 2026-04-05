using Serilog;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /simvar                          — returns latest aircraft position/state as JSON (for iframe)
    /// GET /simvar?lat=&lon=&hdg=&alt=&spd= — stores new values from MSFS panel shell, returns 204
    ///
    /// The MSFS panel shell (CustomPanel.js) can call SimVar but Coherent GT blocks cross-origin POSTs.
    /// Using a plain GET with query-string params avoids the CORS preflight entirely.
    /// The iframe polls GET /simvar (no params) to read the latest values.
    /// </summary>
    internal class SimVarHandler
    {
        private static readonly object _lock = new object();
        private static string _latestJson = "{\"lat\":0,\"lon\":0,\"hdg\":0,\"alt\":0,\"spd\":0}";

        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var query = context.Request.QueryString;
                var hasParams = query["lat"] != null;

                if (hasParams)
                {
                    // Panel shell is writing new SimVar values via query string
                    double.TryParse(query["lat"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat);
                    double.TryParse(query["lon"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon);
                    double.TryParse(query["hdg"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hdg);
                    double.TryParse(query["alt"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var alt);
                    double.TryParse(query["spd"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var spd);
                    var json = $"{{\"lat\":{lat},\"lon\":{lon},\"hdg\":{hdg},\"alt\":{alt},\"spd\":{spd}}}";
                    lock (_lock) { _latestJson = json; }
                    Log.Debug("SimVarHandler: update received — {Json}", json);
                    context.Response.StatusCode = 204;
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                }
                else
                {
                    // iframe is polling for latest values
                    string json;
                    lock (_lock) { json = _latestJson; }
                    Log.Debug("SimVarHandler: GET — serving {Json}", json);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
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
