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
        private static DateTime _validTo = DateTime.MinValue;
        private static readonly HttpClient httpClient = new HttpClient();

        // Preserves the original template with {AIRAC}/{TILEKEY} placeholders
        // so it can be re-resolved after a refresh.
        private string _templateWithPlaceholders;

        public SkyVectorTileSource() : base()
        {
            RefreshIfExpired();
        }

        private void RefreshIfExpired()
        {
            if (_airac != null && _tileServerKey != null && DateTime.UtcNow < _validTo)
                return;

            try
            {
                var apiTask = FetchSkyVectorApiData();
                apiTask.Wait();
                (_airac, _tileServerKey, _validTo) = apiTask.Result;
                Log.Information("Updated SkyVector AIRAC to {Airac}, tile server key: {Key}, valid to: {ValidTo}",
                    _airac, _tileServerKey, _validTo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred during SkyVector API fetch");
                if (_airac == null)
                {
                    // First-run fallback; if we already have data from a previous fetch, keep it
                    var lastMonth = DateTime.Now.AddMonths(-1);
                    _airac = $"{lastMonth.Year - 2000:D2}{lastMonth.Month:D2}";
                    _tileServerKey = null;
                    _validTo = DateTime.UtcNow.AddDays(1); // retry tomorrow
                    Log.Information("Could not fetch SkyVector data. Falling back to AIRAC: {Airac}", _airac);
                }
            }
        }

        private async Task<(string airac, string tileServerKey, DateTime validTo)> FetchSkyVectorApiData()
        {
            var json = await httpClient.GetStringAsync("https://skyvector.com/api/chartDataFPL");
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(json);

            var airac = jsonObject.GetProperty("edition").ToString();

            var tileServersRaw = jsonObject.GetProperty("tileservers").ToString();
            var firstServer = tileServersRaw.Split(',')[0].Trim();
            // FIX: Avoid using C# 8.0 index-from-end operator for compatibility
            var splitParts = firstServer.TrimEnd('/').Split('/');
            var key = splitParts[splitParts.Length - 1];

            // Parse validto: "2026-04-16 09:01:00" (UTC)
            DateTime validTo = DateTime.UtcNow.AddDays(28); // safe fallback
            if (jsonObject.TryGetProperty("validto", out var validToToken) &&
                DateTime.TryParse(validToToken.GetString(), out var parsed))
            {
                validTo = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return (airac, key, validTo);
        }

        /// <summary>
        /// Gets the image Uri for the specified tile indices and zoom level.
        /// Replaces zoomLevel with SkyVector-compatible zoom level and resolves
        /// {AIRAC} and {TILEKEY} placeholders, refreshing from the API when expired.
        /// </summary>
        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            // Capture original template once; UriTemplate is set by MapProvidersDictionary.xaml
            if (_templateWithPlaceholders == null)
                _templateWithPlaceholders = UriTemplate;

            // Re-check expiry; no-op when still valid
            RefreshIfExpired();

            // Resolve placeholders into UriTemplate so base.GetUri can use it
            UriTemplate = _templateWithPlaceholders
                .Replace("{AIRAC}", _airac ?? "")
                .Replace("{TILEKEY}", _tileServerKey ?? "");

            string pattern = @"https://t\.skyvector\.com/.+/(30\d)/\d+/\{z\}/\{x\}/\{y\}\.jpg";
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
