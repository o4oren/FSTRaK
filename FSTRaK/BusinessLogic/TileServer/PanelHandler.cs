using Serilog;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /panel
    /// Serves the moving map panel HTML from the msfs-addon folder alongside the executable.
    /// The MSFS toolbar panel iframe loads this URL when the panel is opened.
    /// </summary>
    internal class PanelHandler
    {
        private static readonly string PanelPath = ResolvePanelPath();

        private static string ResolvePanelPath()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(exeDir, "msfs-addon", "FSTrAkMovingMap.html");
        }

        public async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                if (!File.Exists(PanelPath))
                {
                    Log.Warning("PanelHandler: panel file not found at {Path}", PanelPath);
                    context.Response.StatusCode = 404;
                    context.Response.OutputStream.Close();
                    return;
                }

                var html = File.ReadAllText(PanelPath, Encoding.UTF8);
                var bytes = Encoding.UTF8.GetBytes(html);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PanelHandler: error serving panel");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }
    }
}
