using System;
using MapControl;

namespace FSTRaK.Utils
{
    public class MBTilesTileSource : TileSource
    {
        private readonly string _key;

        public MBTilesTileSource(string key)
        {
            _key = key;
        }

        public override Uri GetUri(int column, int row, int zoomLevel)
        {
            return new Uri($"http://localhost:{MBTilesLocalServer.Port}/mbtiles/{_key}/{zoomLevel}/{column}/{row}");
        }
    }
}
