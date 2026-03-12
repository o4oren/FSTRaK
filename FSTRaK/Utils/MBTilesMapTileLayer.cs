using System.IO;
using System.Reflection;
using MapControl;
using Serilog;

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
                    Log.Debug("MBTiles resolved path: {Path}, exists: {Exists}", resolved, File.Exists(resolved));
                    MBTilesLocalServer.Start();
                    var key = MBTilesLocalServer.Register(resolved);
                    TileSource = new MBTilesTileSource(key);
                }
                else
                {
                    TileSource = null;
                }
            }
        }
    }
}
