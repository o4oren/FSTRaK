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

            // Insert OpenAIP above base
            var openAipLayer = MapProviderResolver.GetOpenAipLayer();
            if (openAipLayer != null)
            {
                var baseIndex = map.Children.IndexOf(baseLayer);
                if (baseIndex >= 0)
                    map.Children.Insert(baseIndex + 1, openAipLayer);
                else
                    map.Children.Add(openAipLayer);
                currentOpenAipLayer = openAipLayer;
            }

            // Insert chart overlay as topmost layer
            var chartLayer = MapProviderResolver.GetChartOverlayProvider();
            if (chartLayer != null)
            {
                map.Children.Add(chartLayer);
                currentChartLayer = chartLayer;
            }
        }
    }
}
