using MapControl;
using System;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class OpenFlightMapsMapTileLayer : MapTileLayer
    {
        public OpenFlightMapsMapTileLayer() : base()
        {
        }

        /// <summary>
        /// Computes the current AIRAC cycle string in "YYMM" format, e.g. "2603".
        /// AIRAC epoch: 2025-01-23 = cycle 2501. Each cycle is 28 days.
        /// </summary>
        public static string GetCurrentAiracCycle()
        {
            var epoch = new DateTime(2025, 1, 23); // cycle 2501
            var today = DateTime.UtcNow.Date;

            int cycleYear = 2025;
            int cycleWithinYear = 1;
            DateTime cycleStart = epoch;

            while (cycleStart.AddDays(28) <= today)
            {
                cycleStart = cycleStart.AddDays(28);
                cycleWithinYear++;
                // When we cross into a new calendar year, reset cycle counter
                if (cycleStart.Year > cycleYear)
                {
                    cycleYear = cycleStart.Year;
                    cycleWithinYear = 1;
                }
            }

            return $"{cycleYear % 100:D2}{cycleWithinYear:D2}";
        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource?.UriTemplate != null &&
                TileSource.UriTemplate.Contains("{AiracCycle}"))
            {
                TileSource.UriTemplate =
                    TileSource.UriTemplate.Replace("{AiracCycle}", GetCurrentAiracCycle());
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
