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
    }
}
