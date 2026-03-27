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
        private static bool _fetchInProgress;
        private static readonly HttpClient httpClient = new HttpClient();

        // Preserves the original template with {AIRAC}/{TILEKEY} placeholders
        // so it can be re-resolved after a refresh.
        private string _templateWithPlaceholders;

        public SkyVectorTileSource() : base()
        {
            // Kick off background fetch without blocking — tiles will resolve
            // once data arrives; until then GetUri returns null (no tiles shown).
            if (_airac == null || (_tileServerKey == null && !_fetchInProgress))
                StartBackgroundFetch();
        }

        private static void StartBackgroundFetch()
        {
            _fetchInProgress = true;
            Task.Run(async () =>
            {
                try
                {
                    var result = await FetchSkyVectorApiData();
                    _airac = result.airac;
                    _tileServerKey = result.tileServerKey;
                    _validTo = result.validTo;
                    Log.Information("Updated SkyVector AIRAC to {Airac}, tile server key: {Key}, valid to: {ValidTo}",
                        _airac, _tileServerKey, _validTo);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred during SkyVector API fetch");
                    if (_airac == null)
                    {
                        var lastMonth = DateTime.Now.AddMonths(-1);
                        _airac = $"{lastMonth.Year - 2000:D2}{lastMonth.Month:D2}";
                        _tileServerKey = null;
                        _validTo = DateTime.UtcNow.AddDays(1);
                        Log.Information("Could not fetch SkyVector data. Falling back to AIRAC: {Airac}", _airac);
                    }
                }
                finally
                {
                    _fetchInProgress = false;
                }
            });
        }

        private static async Task<(string airac, string tileServerKey, DateTime validTo)> FetchSkyVectorApiData()
        {
            var json = await httpClient.GetStringAsync("https://skyvector.com/api/chartDataFPL");
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(json);

            var airac = jsonObject.GetProperty("edition").ToString();

            var tileServersRaw = jsonObject.GetProperty("tileservers").ToString();
            var firstServer = tileServersRaw.Split(',')[0].Trim();
            var splitParts = firstServer.TrimEnd('/').Split('/');
            var key = splitParts[splitParts.Length - 1];

            DateTime validTo = DateTime.UtcNow.AddDays(28);
            if (jsonObject.TryGetProperty("validto", out var validToToken) &&
                DateTime.TryParse(validToToken.GetString(), out var parsed))
            {
                validTo = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return (airac, key, validTo);
        }

        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            // Capture original template once
            if (_templateWithPlaceholders == null)
                _templateWithPlaceholders = UriTemplate;

            // Kick off a refresh in background if data has expired
            if (_airac != null && _tileServerKey != null
                && DateTime.UtcNow >= _validTo && !_fetchInProgress)
                StartBackgroundFetch();

            // Data not yet available — return null (MapControl skips null URIs)
            if (_airac == null || _tileServerKey == null)
                return null;

            UriTemplate = _templateWithPlaceholders
                .Replace("{AIRAC}", _airac)
                .Replace("{TILEKEY}", _tileServerKey);

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
