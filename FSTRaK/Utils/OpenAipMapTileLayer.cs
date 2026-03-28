using MapControl;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class OpenAipMapTileLayer : MapTileLayer
    {
        public static string ApiKey;

        public OpenAipMapTileLayer() : base()
        {
        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource.UriTemplate.Contains("{ApiKey}"))
            {
                TileSource.UriTemplate = TileSource.UriTemplate.Replace("{ApiKey}", OpenAipMapTileLayer.ApiKey);
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
