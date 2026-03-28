using MapControl;

namespace FSTRaK.Utils
{
    public static class MapLayerHelper
    {
        public static void UpdateMapLayers(
            MapBase map,
            ref MapTileLayerBase currentOpenAipLayer,
            ref MapTileLayerBase currentChartLayer)
        {
            // Tear down existing overlay layers
            if (currentOpenAipLayer != null)
            {
                map.Children.Remove(currentOpenAipLayer);
                currentOpenAipLayer = null;
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

            // Insert OpenAIP above base
            var openAipLayer = MapProviderResolver.GetOpenAipLayer();
            if (openAipLayer != null)
            {
                map.Children.Insert(insertAt, openAipLayer);
                currentOpenAipLayer = openAipLayer;
                insertAt++;
            }

            // Insert chart overlay above OpenAIP (or above base)
            var chartLayer = MapProviderResolver.GetChartOverlayProvider();
            if (chartLayer != null)
            {
                map.Children.Insert(insertAt, chartLayer);
                currentChartLayer = chartLayer;
            }
        }
    }
}
