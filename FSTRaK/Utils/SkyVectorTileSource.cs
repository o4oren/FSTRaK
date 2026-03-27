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
        private static string _tileServerKey;
        private static readonly HttpClient httpClient = new HttpClient();

        public SkyVectorTileSource() : base()
        {
            if (_airac == null || _tileServerKey == null)
            {
                try
                {
                    var apiTask = FetchSkyVectorApiData();
                    apiTask.Wait();
                    (_airac, _tileServerKey) = apiTask.Result;
                    Log.Information("Updated SkyVector AIRAC to {Airac}, tile server key: {Key}", _airac, _tileServerKey);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred during SkyVector API fetch");
                    // Best effort fallback
                    var lastMonth = DateTime.Now.AddMonths(-1);
                    _airac = $"{lastMonth.Year - 2000:D2}{lastMonth.Month:D2}";
                    _tileServerKey = null;
                    Log.Information("Could not update SkyVector data. Falling back to AIRAC: {Airac}", _airac);
                }
            }
        }

        private async Task<(string airac, string tileServerKey)> FetchSkyVectorApiData()
        {
            var json = await httpClient.GetStringAsync("https://skyvector.com/api/chartDataFPL");
            var jsonObject = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
            var airac = jsonObject.GetProperty("edition").ToString();
            // tileservers is a comma-separated list of base URLs; take the first
            var tileServersRaw = jsonObject.GetProperty("tileservers").ToString();
            var firstServer = tileServersRaw.Split(',')[0].Trim();
            // Extract the key: last path segment of https://t.skyvector.com/{key}
            var key = firstServer.TrimEnd('/').Split('/')[^1];
            return (airac, key);
        }

        /// <summary>
        /// Gets the image Uri for the specified tile indices and zoom level.
        /// Replaces zoomLevel with skyvector compatible zoomLevel
        /// </summary>
        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            if (UriTemplate.Contains("{AIRAC}"))
                UriTemplate = UriTemplate.Replace("{AIRAC}", _airac);

            if (_tileServerKey != null && UriTemplate.Contains("{TILEKEY}"))
                UriTemplate = UriTemplate.Replace("{TILEKEY}", _tileServerKey);

            string pattern = @"https:\/\/t.skyvector.com\/.+\/(30\d)\/\d+\/\{z}\/{x}\/\{y}\.jpg";
            Match m = Regex.Match(UriTemplate, pattern);
            int newZoomLevel = zoomLevel;

            if (m.Success && m.Groups.Count > 1)
            {
                var chartTypeNumber = int.Parse(m.Groups[1].Value);
                newZoomLevel = 23 + 301 - chartTypeNumber - (2 * zoomLevel);
            }
            return base.GetUri(column, row, newZoomLevel);
        }
    }
}
