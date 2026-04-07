using System.Windows;
using MapControl;

namespace FSTRaK.Utils
{
    public class MapProviderResolver
    {
        public static MapTileLayerBase GetMapProvider()
        {
            var resourceKey = Properties.Settings.Default.MapTileProvider;
            var resource = Application.Current.Resources[resourceKey] as MapTileLayerBase;
            if (resource != null)
                return resource;

            return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
        }

        public static MapTileLayerBase GetChartOverlayProvider()
        {
            var key = Properties.Settings.Default.ChartOverlayProvider;
            if (string.IsNullOrEmpty(key) || key == "None") return null;
            return Application.Current.Resources[key] as MapTileLayerBase;
        }

        public static MapTileLayerBase GetAeroOverlayLayer()
        {
            if (Properties.Settings.Default.IsOpenAipEnabled)
                return Application.Current.Resources["OpenAIP"] as MapTileLayerBase;
            if (Properties.Settings.Default.IsOpenFlightMapsEnabled)
                return Application.Current.Resources["Open Flightmaps"] as MapTileLayerBase;
            return null;
        }
    }
}
