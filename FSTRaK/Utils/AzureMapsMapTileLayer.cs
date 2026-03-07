using MapControl;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class AzureMapsMapTileLayer : MapTileLayer
    {
        public static string ApiKey;

        public AzureMapsMapTileLayer() : base()
        {
        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource.UriTemplate.Contains("{ApiKey}"))
            {
                TileSource.UriTemplate = TileSource.UriTemplate.Replace("{ApiKey}", AzureMapsMapTileLayer.ApiKey);
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
