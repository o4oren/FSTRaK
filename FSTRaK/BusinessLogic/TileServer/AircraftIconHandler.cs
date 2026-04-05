using FSTRaK.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /aircraft-icon
    /// Returns an SVG of the current aircraft icon, using the same geometry and scale as the FSTRaK live map.
    /// The panel HTML polls this to keep the icon in sync with the loaded aircraft type.
    /// </summary>
    internal class AircraftIconHandler
    {
        private static readonly Dictionary<string, string> _geometries = LoadGeometries();

        private static Dictionary<string, string> LoadGeometries()
        {
            var result = new Dictionary<string, string>();
            try
            {
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(exeDir, "Resources", "AircraftIconsDictionary.xaml");
                if (!File.Exists(path))
                {
                    Log.Warning("AircraftIconHandler: dictionary not found at {Path}", path);
                    return result;
                }
                Log.Debug("AircraftIconHandler: loading from {Path}", path);
                var xaml = File.ReadAllText(path);
                var matches = Regex.Matches(xaml, @"x:Key=""([^""]+)""\s+x:Shared=""True"">([^<]+)<");
                foreach (Match m in matches)
                    result[m.Groups[1].Value] = m.Groups[2].Value.Trim();
                Log.Debug("AircraftIconHandler: loaded {Count} geometries", result.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AircraftIconHandler: failed to load geometry dictionary");
            }
            return result;
        }

        // Fallback generic airplane path used when AircraftIconsDictionary.xaml is unavailable
        private const string FallbackPath = "M16 2 L19 26 L16 23 L13 26 Z M8 15 L16 13 L24 15 L24 18 L16 16 L8 18 Z M11 27 L16 25 L21 27 L21 29 L16 27.5 L11 29 Z";

        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var aircraft = FlightManager.FlightManager.Instance.ActiveFlight?.Aircraft;
                var (iconKey, scale) = aircraft != null
                    ? AircraftResolver.GetAircraftIcon(aircraft)
                    : ("B737", 0.75);

                if (!_geometries.TryGetValue(iconKey, out var pathData) || string.IsNullOrEmpty(pathData))
                {
                    if (!_geometries.TryGetValue("B737", out pathData) || string.IsNullOrEmpty(pathData))
                        pathData = FallbackPath;
                    Log.Debug("AircraftIconHandler: geometry for {Key} not found, using fallback. Loaded keys: {Count}", iconKey, _geometries.Count);
                }

                // Geometry is in a 32×32 coordinate space; scale and center in a 48×48 SVG
                var size = 48;
                var center = size / 2.0;
                var svgScale = (size / 32.0) * scale;
                var translate = center - (16.0 * svgScale);

                var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"" viewBox=""0 0 {size} {size}"">
  <g transform=""translate({translate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},{translate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}) scale({svgScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)})"">
    <path d=""{pathData}"" fill=""red"" stroke=""darkred"" stroke-width=""0.5""/>
  </g>
</svg>";

                var bytes = Encoding.UTF8.GetBytes(svg);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "image/svg+xml";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Cache-Control", "no-cache");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AircraftIconHandler error");
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
