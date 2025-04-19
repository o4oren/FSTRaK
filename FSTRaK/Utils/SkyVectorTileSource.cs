using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MapControl;
using Serilog;

namespace FSTRaK.Utils
{
    public class SkyVectorTileSource : TileSource
    {

        private static string _airac;
        private static HttpClient httpClient = new()
        {
            BaseAddress = new Uri("https://skyvector.com/api/chartDataFPL"),
        };

        public SkyVectorTileSource() : base()
        {
            if (_airac == null)
            {
                try
                {
                    var airacTask = GetAiracFromSkyVector();
                    airacTask.Wait();
                    _airac = airacTask.Result;
                    Log.Information("Updated SkyVector Airac to " + _airac);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occured during airac fetch");
                    // best effort - use current month - 1
                    var lastMonth = DateTime.Now.AddMonths(-1);
                    var yearPart = lastMonth.Year - 2000;
                    var cycle = lastMonth.Month;
                    _airac = $"{yearPart:D2}{cycle:D2}";
                    Log.Information("Could not update SkyVector Airac. Falling back to: " + _airac);
                }
            }
        }

        private async Task<string> GetAiracFromSkyVector()
        {
            var skyVectorChartData = httpClient.GetStringAsync("");
            skyVectorChartData.Wait();
            var jsonObject = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(skyVectorChartData.Result);
            var edition = jsonObject.GetProperty("edition");
            return edition.ToString();
        }

        /// <summary>
        /// Gets the image Uri for the specified tile indices and zoom level.
        /// Replaces zoomLevel with skyvector compatible zoomLevel
        /// </summary>
        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            if (UriTemplate.Contains("{AIRAC}"))
            {
                UriTemplate = UriTemplate.Replace("{AIRAC}", _airac);
            }
            // Fetching the 301 in this example: https://t.skyvector.com/V7pMh4xRihf1nr61/301/2306/{z}/{x}/{y}.jpg
            // /https:\/\/t.skyvector.com\/.+\/(30\d)\/\d+\/\d+\/\d+\/\d+\.jpg/gm
             string pattern = @"https:\/\/t.skyvector.com\/.+\/(30\d)\/\d+\/\{z}\/{x}\/\{y}\.jpg";
             Match m = Regex.Match(UriTemplate, pattern);
             int newZoomLevel = zoomLevel;

            if (m.Groups.Count > 0)
            {
                 var chartTypeString = m.Groups[1].Value;
                 var chartTypeNumber = int.Parse(chartTypeString);
                 newZoomLevel = 23 + 301 - chartTypeNumber - (2 * zoomLevel);
            }
            var uri = base.GetUri(column, row, newZoomLevel);
            return base.GetUri(column, row, newZoomLevel);
        }
    }
}
