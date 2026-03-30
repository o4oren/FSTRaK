using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FSTRaK.Utils
{
    public static class AirlineLogoResolver
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new ConcurrentDictionary<string, BitmapImage>();
        // Sentinel stored in cache to represent "no logo found" — avoids repeated disk checks
        private static readonly BitmapImage _notFound = new BitmapImage();

        /// <summary>
        /// Returns a BitmapImage for the airline identified by the first 3 characters of
        /// the callsign, or null if no logo is available.
        /// </summary>
        public static BitmapImage GetLogo(string callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign) || callsign.Length < 3) return null;
            var prefix = callsign.Substring(0, 3).ToUpperInvariant();
            if (_cache.TryGetValue(prefix, out var cached))
                return ReferenceEquals(cached, _notFound) ? null : cached;
            var logo = TryLoad(prefix);
            _cache[prefix] = logo ?? _notFound;
            return logo;
        }

        private static BitmapImage TryLoad(string prefix)
        {
            foreach (var ext in new[] { "png", "jpg" })
            {
                var uri = new Uri($"pack://application:,,,/FSTRaK;component/Assets/AirlineLogos/{prefix}.{ext}");
                var stream = Application.GetResourceStream(uri);
                if (stream == null) continue;
                stream.Stream.Dispose();
                return LoadFromUri(uri);
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
