using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapControl;

namespace FSTRaK.Utils
{
    public class CoordinatesUtil
    {
        public static Location CalculateCenter(List<LocationCollection> locations)
        {
            double sumLat = 0.0;
            double sumLon = 0.0;
            int totalVertices = 0;

            foreach (var polygon in locations)
            {
                foreach (var vertex in polygon)
                {
                    sumLat += vertex.Latitude;
                    sumLon += vertex.Longitude;
                    totalVertices++;
                }
            }

            double centerLat = sumLat / totalVertices;
            double centerLon = sumLon / totalVertices;

            return new Location(centerLat,centerLon);
        }
    }
}
