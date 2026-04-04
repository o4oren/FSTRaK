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
    ///
    /// Provider objects (MapTileLayerBase) are DependencyObjects and must only be accessed
    /// on the UI thread. This handler reads UriTemplate inside Dispatcher.InvokeAsync,
    /// then passes the resolved URL string (and MBTiles layer ref) to TileProxyService,
    /// which runs entirely off the UI thread.
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

            try
            {
                if (route.StartsWith("base/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseZXY(parts, 1, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    var providerKey = FSTRaK.Properties.Settings.Default.MapTileProvider;
                    var (url, mbSource) = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var p = MapProviderResolver.GetMapProvider();
                        if (p is MBTilesMapTileLayer mb) return ((string)null, mb.TileSource as MBTilesTileSource);
                        return (ResolveUrl(p, z, x, y), (MBTilesTileSource)null);
                    });

                    await ServeTile(context, url, mbSource, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/chart/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    var providerKey = FSTRaK.Properties.Settings.Default.ChartOverlayProvider;
                    var (url, mbSource) = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var p = MapProviderResolver.GetChartOverlayProvider();
                        if (p == null) return ((string)null, (MBTilesTileSource)null);
                        if (p is MBTilesMapTileLayer mb) return ((string)null, mb.TileSource as MBTilesTileSource);
                        return (ResolveUrl(p, z, x, y), (MBTilesTileSource)null);
                    });

                    if (url == null && mbSource == null) { Respond404(context); return; }
                    await ServeTile(context, url, mbSource, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/openaip/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!FSTRaK.Properties.Settings.Default.IsOpenAipEnabled)
                    { Respond404(context); return; }

                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    var url = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var p = MapProviderResolver.GetOpenAipLayer();
                        return p == null ? null : ResolveUrl(p, z, x, y);
                    });

                    if (url == null) { Respond404(context); return; }
                    await ServeTile(context, url, null, "OpenAIP", z, x, y);
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

        /// <summary>Resolves the upstream tile URL from a web provider. Must be called on the UI thread.</summary>
        private static string ResolveUrl(MapControl.MapTileLayerBase provider, int z, int x, int y)
        {
            var template = provider?.TileSource?.UriTemplate;
            if (string.IsNullOrEmpty(template)) return null;
            return template
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());
        }

        private async Task ServeTile(HttpListenerContext context, string url, MBTilesTileSource mbSource,
            string providerKey, int z, int x, int y)
        {
            byte[] bytes;
            if (mbSource != null)
                bytes = await _proxy.GetMBTilesBytesAsync(mbSource, z, x, y);
            else
                bytes = await _proxy.GetWebTileBytesAsync(url, providerKey, z, x, y);
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
