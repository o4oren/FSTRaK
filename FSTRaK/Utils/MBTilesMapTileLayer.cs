using System.IO;
using System.Reflection;
using MapControl;

namespace FSTRaK.Utils
{
    public class MBTilesMapTileLayer : MapTileLayer
    {
        private string _filePath;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                if (!string.IsNullOrEmpty(value))
                {
                    var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (exeDir == null) return;
                    var resolved = Path.Combine(exeDir, "Resources", "Data", value);
                    TileSource = new MBTilesTileSource(resolved);
                }
                else
                {
                    TileSource = null;
                }
            }
        }
    }
}
