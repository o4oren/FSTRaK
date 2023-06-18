using System;
using System.Text.RegularExpressions;
using MapControl;
using Serilog;

namespace FSTRaK.Utils
{
    public class SkyVectorTileSource : TileSource
    {



        /// <summary>
        /// Gets the image Uri for the specified tile indices and zoom level.
        /// Replaces zoomLevel with skyvector compatible zoomLevel
        /// </summary>
        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            // Fetching the 301 in this example: https://t.skyvector.com/V7pMh4xRihflnr61/301/2306/{z}/{x}/{y}.jpg
            // /https:\/\/t.skyvector.com\/.+\/(30\d)\/\d+\/\d+\/\d+\/\d+\.jpg/gm
             string pattern = @"https:\/\/t.skyvector.com\/.+\/(30\d)\/\d+\/\{z}\/{x}\/\{y}\.jpg";
             Match m = Regex.Match(UriTemplate, pattern);
             int newZoomLevel = zoomLevel;

            if (m.Groups.Count > 0)
            {
                 var chartTypeString = m.Groups[1].Value;
                 Log.Information("AF chart " + chartTypeString);
                 var chartTypeNumber = int.Parse(chartTypeString);
                 newZoomLevel = 23 + 301 - chartTypeNumber - (2 * zoomLevel);
            }
            return base.GetUri(column, row, newZoomLevel);
        }
    }
}
