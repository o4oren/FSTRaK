using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

    // TRACON boundary model classes

    internal class TraconPrefixConverter : JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return new List<string> { (string)reader.Value };
            var list = new List<string>();
            serializer.Populate(reader, list);
            return list;
        }

        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
            => serializer.Serialize(writer, value);
    }

    public class TraconGeoJsonProperties
    {
        public string id { get; set; }
        public string name { get; set; }
        [JsonConverter(typeof(TraconPrefixConverter))]
        public List<string> prefix { get; set; }
        public string suffix { get; set; }
    }

    public class TraconGeoJsonFeature
    {
        public TraconGeoJsonProperties Properties { get; set; }
        public GeoJsonGeometry Geometry { get; set; }
    }

    public class TraconGeoJsonFeatureCollection
    {
        public TraconGeoJsonFeature[] Features { get; set; }
    }
}
