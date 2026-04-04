using FSTRaK.Utils;
using MapControl;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Resolves tile bytes for a given provider and {z}/{x}/{y}.
    /// Web providers: fetches upstream URL (with API key already in UriTemplate) and caches in LRU.
    /// MBTiles providers: reads SQLite directly via MBTilesTileSource.
    /// </summary>
    internal class TileProxyService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static TileProxyService()
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "FSTrAk - Flight Simulator logbook and tracker");
        }

        // LRU cache: keyed "providerKey:z/x/y" → raw PNG/JPEG bytes
        // Max 500 entries. Access order tracked via LinkedList.
        private const int CacheCapacity = 500;
        private readonly ConcurrentDictionary<string, byte[]> _cacheData = new ConcurrentDictionary<string, byte[]>();
        private readonly LinkedList<string> _cacheOrder = new LinkedList<string>();
        private readonly object _cacheLock = new object();

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cacheData.Clear();
                _cacheOrder.Clear();
            }
        }

        /// <summary>
        /// Returns raw tile bytes, or null if the tile is not available.
        /// providerKey is used as cache namespace (e.g. "OpenStreetMap", "CVFR").
        /// </summary>
        public async Task<byte[]> GetTileAsync(MapTileLayerBase provider, string providerKey, int z, int x, int y)
        {
            if (provider == null) return null;

            if (provider is MBTilesMapTileLayer mbLayer)
                return await GetMBTileAsync(mbLayer, z, x, y);

            return await GetWebTileAsync(provider, providerKey, z, x, y);
        }

        private async Task<byte[]> GetWebTileAsync(MapTileLayerBase provider, string providerKey, int z, int x, int y)
        {
            var cacheKey = $"{providerKey}:{z}/{x}/{y}";

            // Check cache
            lock (_cacheLock)
            {
                if (_cacheData.TryGetValue(cacheKey, out var cached))
                {
                    _cacheOrder.Remove(cacheKey);
                    _cacheOrder.AddLast(cacheKey);
                    return cached;
                }
            }

            // Resolve upstream URL from provider's UriTemplate (API key already substituted)
            var uriTemplate = provider.TileSource?.UriTemplate;
            if (string.IsNullOrEmpty(uriTemplate)) return null;

            var url = uriTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            try
            {
                using (var response = await _httpClient.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Debug("TileProxyService: upstream returned {Status} for {Url}", (int)response.StatusCode, url);
                        return null;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    AddToCache(cacheKey, bytes);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TileProxyService: failed to fetch {Url}", url);
                return null;
            }
        }

        private async Task<byte[]> GetMBTileAsync(MBTilesMapTileLayer layer, int z, int x, int y)
        {
            if (layer.TileSource == null) return null;
            var source = layer.TileSource as MBTilesTileSource;
            if (source == null) return null;
            try
            {
                return await source.GetRawBytesAsync(x, y, z);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TileProxyService: MBTiles read failed for {z}/{x}/{y}", z, x, y);
                return null;
            }
        }

        private void AddToCache(string key, byte[] bytes)
        {
            lock (_cacheLock)
            {
                if (_cacheData.ContainsKey(key))
                {
                    _cacheOrder.Remove(key);
                    _cacheOrder.AddLast(key);
                    _cacheData[key] = bytes;
                    return;
                }

                if (_cacheOrder.Count >= CacheCapacity)
                {
                    var oldest = _cacheOrder.First.Value;
                    _cacheOrder.RemoveFirst();
                    _cacheData.TryRemove(oldest, out _);
                }

                _cacheData[key] = bytes;
                _cacheOrder.AddLast(key);
            }
        }
    }
}
