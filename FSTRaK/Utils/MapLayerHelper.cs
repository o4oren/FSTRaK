using MapControl;

namespace FSTRaK.Utils
{
    public static class MapLayerHelper
    {
        public static void UpdateMapLayers(
            MapBase map,
            ref MapTileLayerBase currentAeroOverlayLayer,
            ref MapTileLayerBase currentChartLayer)
        {
            // Tear down existing overlay layers
            if (currentAeroOverlayLayer != null)
            {
                map.Children.Remove(currentAeroOverlayLayer);
                currentAeroOverlayLayer = null;
            }
            if (currentChartLayer != null)
            {
                map.Children.Remove(currentChartLayer);
                currentChartLayer = null;
            }

            // Set base layer
            var baseLayer = MapProviderResolver.GetMapProvider();
            if (baseLayer == null) return;
            map.MapLayer = baseLayer;

            // Determine insertion point: just after the base layer
            var baseIndex = map.Children.IndexOf(baseLayer);
            var insertAt = baseIndex >= 0 ? baseIndex + 1 : 0;

            // Insert aero overlay (OpenAIP or OFM) above base
            var aeroLayer = MapProviderResolver.GetAeroOverlayLayer();
            if (aeroLayer != null)
            {
                map.Children.Insert(insertAt, aeroLayer);
                currentAeroOverlayLayer = aeroLayer;
                insertAt++;
            }

            // Insert chart overlay above aero overlay (or above base)
            var chartLayer = MapProviderResolver.GetChartOverlayProvider();
            if (chartLayer != null)
            {
                map.Children.Insert(insertAt, chartLayer);
                currentChartLayer = chartLayer;
            }
        }
    }
}
