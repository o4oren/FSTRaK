using MapControl;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class MapTilerMapTileLayer : MapTileLayer
    {
        public static string ApiKey;

        public MapTilerMapTileLayer() : base()
        {

        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource.UriTemplate.Contains("{ApiKey}"))
            {
                TileSource.UriTemplate = TileSource.UriTemplate.Replace("{ApiKey}", MapTilerMapTileLayer.ApiKey);
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
