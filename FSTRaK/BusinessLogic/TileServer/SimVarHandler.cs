using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET  /simvar — returns latest aircraft position/state posted by the MSFS panel shell
    /// POST /simvar — receives aircraft position/state from the MSFS panel shell (coui:// origin)
    ///
    /// The MSFS panel shell (CustomPanel.js) can call SimVar but cannot reach the iframe via postMessage
    /// due to cross-origin restrictions. Instead it POSTs SimVar data here, and the iframe polls GET.
    /// </summary>
    internal class SimVarHandler
    {
        private static readonly object _lock = new object();
        private static string _latestJson = "{\"lat\":0,\"lon\":0,\"hdg\":0,\"alt\":0,\"spd\":0}";

        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var method = context.Request.HttpMethod.ToUpperInvariant();

                if (method == "POST")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        var body = reader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            // Validate it's parseable JSON before storing
                            JObject.Parse(body);
                            lock (_lock) { _latestJson = body; }
                            Log.Debug("SimVarHandler: POST received — {Body}", body);
                        }
                        else
                        {
                            Log.Warning("SimVarHandler: POST received with empty body");
                        }
                    }
                    context.Response.StatusCode = 204;
                }
                else
                {
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
