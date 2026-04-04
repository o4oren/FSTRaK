using FSTRaK.Utils;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Handles tile HTTP requests:
    ///   GET /tiles/base/{z}/{x}/{y}
    ///   GET /tiles/overlay/chart/{z}/{x}/{y}
    ///   GET /tiles/overlay/openaip/{z}/{x}/{y}
    /// </summary>
    internal class TileHandler
    {
        private readonly TileProxyService _proxy;

        public TileHandler(TileProxyService proxy)
        {
            _proxy = proxy;
        }

        public async Task HandleAsync(HttpListenerContext context, string route)
        {
            // route examples: "base/5/12/10"  "overlay/chart/5/12/10"  "overlay/openaip/5/12/10"
            var parts = route.TrimStart('/').Split('/');

            MapControl.MapTileLayerBase provider = null;
            string providerKey = null;

            try
            {
                // Determine provider type from route prefix
                if (route.StartsWith("base/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["base", z, x, y]
                    provider = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => MapProviderResolver.GetMapProvider());
                    providerKey = FSTRaK.Properties.Settings.Default.MapTileProvider;

                    if (!TryParseZXY(parts, 1, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/chart/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["overlay", "chart", z, x, y]
                    provider = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => MapProviderResolver.GetChartOverlayProvider());
                    if (provider == null) { Respond404(context); return; }
                    providerKey = FSTRaK.Properties.Settings.Default.ChartOverlayProvider;

                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/openaip/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["overlay", "openaip", z, x, y]
                    if (!FSTRaK.Properties.Settings.Default.IsOpenAipEnabled)
                    { Respond404(context); return; }

                    provider = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => MapProviderResolver.GetOpenAipLayer());
                    if (provider == null) { Respond404(context); return; }
                    providerKey = "OpenAIP";

                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else
                {
                    Respond404(context);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TileHandler: unhandled error for route {Route}", route);
                Respond500(context);
            }
        }

        private async Task ServeTile(HttpListenerContext context, MapControl.MapTileLayerBase provider,
            string providerKey, int z, int x, int y)
        {
            var bytes = await _proxy.GetTileAsync(provider, providerKey, z, x, y);
            if (bytes == null || bytes.Length == 0)
            {
                Respond404(context);
                return;
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = DetectContentType(bytes);
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.ContentLength64 = bytes.Length;
            try
            {
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static string DetectContentType(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";
            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";
            if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "image/webp";
            return "application/octet-stream";
        }

        private static bool TryParseZXY(string[] parts, int offset, out int z, out int x, out int y)
        {
            z = x = y = 0;
            return parts.Length >= offset + 3
                && int.TryParse(parts[offset], out z)
                && int.TryParse(parts[offset + 1], out x)
                && int.TryParse(parts[offset + 2], out y);
        }

        private static void Respond404(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        }

        private static void Respond500(HttpListenerContext context)
        {
            context.Response.StatusCode = 500;
            context.Response.OutputStream.Close();
        }
    }
}
