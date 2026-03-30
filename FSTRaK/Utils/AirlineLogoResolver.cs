using System;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using FSTRaK.DataTypes;

namespace FSTRaK.Utils
{
    public static class AirlineLogoResolver
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new ConcurrentDictionary<string, BitmapImage>();

        /// <summary>
        /// Returns a BitmapImage for the airline identified by the first 3 characters of
        /// the callsign, or null if no logo is available.
        /// </summary>
        public static BitmapImage GetLogo(string callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign) || callsign.Length < 3) return null;
            var prefix = callsign.Substring(0, 3).ToUpperInvariant();
            if (_cache.TryGetValue(prefix, out var cached)) return cached;
            var logo = TryLoad(prefix);
            if (logo != null) _cache[prefix] = logo;
            return logo;
        }

        /// <summary>Returns the network logo BitmapImage for VATSIM or IVAO.</summary>
        public static BitmapImage GetNetworkLogo(NetworkType network)
        {
            var key = network == NetworkType.Vatsim ? "vatsim" : "ivao";
            return _cache.GetOrAdd(key, _ =>
            {
                var uri = new Uri($"pack://application:,,,/FSTRaK;component/Assets/NetworkLogos/{key}.png");
                try { return LoadFromUri(uri); }
                catch { return null; }
            });
        }

        private static BitmapImage TryLoad(string prefix)
        {
            // Try PNG first, then JPG
            foreach (var ext in new[] { "png", "jpg" })
            {
                var uri = new Uri($"pack://application:,,,/FSTRaK;component/Assets/AirlineLogos/{prefix}.{ext}");
                try { return LoadFromUri(uri); }
                catch { }
            }
            return null;
        }

        private static BitmapImage LoadFromUri(Uri uri)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = uri;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
    }
}
