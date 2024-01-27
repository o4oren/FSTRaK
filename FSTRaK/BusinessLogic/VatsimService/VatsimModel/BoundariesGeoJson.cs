using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.VatsimService.VatsimModel
{
    public class GeoJsonFeature
    {
        public string Type { get; set; }
        public GeoJsonProperties Properties { get; set; }
        public GeoJsonGeometry Geometry { get; set; }
    }

    public class GeoJsonProperties
    {
        // Add properties as needed based on your GeoJSON structure
        public string id { get; set; }
        public string oceanic { get; set; }
        public string label_lon { get; set; }
        public string label_lat { get; set; }
        public string region { get; set; }
        public string division { get; set; }
    }

    public class GeoJsonGeometry
    {
        public string Type { get; set; }
        public double[][][][] Coordinates { get; set; }
    }

    public class GeoJsonFeatureCollection
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public GeoJsonCrs Crs { get; set; }
        public GeoJsonFeature[] Features { get; set; }
    }

    public class GeoJsonCrs
    {
        public string Type { get; set; }
        public GeoJsonCrsProperties Properties { get; set; }
    }

    public class GeoJsonCrsProperties
    {
        public string Name { get; set; }
    }
}
