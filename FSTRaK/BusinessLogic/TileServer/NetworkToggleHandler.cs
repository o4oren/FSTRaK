using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// POST /network/atc/toggle
    /// Toggles IsShowAtc in LiveViewViewModel and returns the new state.
    /// </summary>
    internal class NetworkToggleHandler
    {
        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var lvm = App.LiveViewViewModel;
                bool newValue = false;

                if (lvm != null)
                {
                    newValue = Application.Current.Dispatcher.Invoke(() =>
                    {
                        lvm.IsShowAtc = !lvm.IsShowAtc;
                        return lvm.IsShowAtc;
                    });
                }

                var json = new JObject { ["atcVisible"] = newValue }.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NetworkToggleHandler: error toggling ATC");
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
