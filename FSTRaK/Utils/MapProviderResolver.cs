using System.Windows;
using MapControl;

namespace FSTRaK.Utils
{
    public class MapProviderResolver
    {
        public static MapTileLayerBase GetMapProvider()
            
        {
            var resoueceKey = Properties.Settings.Default.MapTileProvider;
            var resource = Application.Current.Resources[resoueceKey] as MapTileLayerBase;
            if (resource != null)
            {
                if (resource.SourceName.StartsWith("SkyVector"))
                {
                    resource.TileSource = new SkyVectorTileSource
                    {
                        UriTemplate = resource.TileSource.UriTemplate,
                    };
                }

                return resource;
            }

            return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
        }
    }
}
