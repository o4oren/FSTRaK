using MapControl;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class StadiaMapsMapTileLayer : MapTileLayer
    {
        public static string ApiKey;

        public StadiaMapsMapTileLayer() : base()
        {

        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource.UriTemplate.Contains("{ApiKey}"))
            {
                TileSource.UriTemplate = TileSource.UriTemplate.Replace("{ApiKey}", StadiaMapsMapTileLayer.ApiKey);
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
