using System;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapControl;
using Serilog;

namespace FSTRaK.Utils
{
    public class MBTilesTileSource : TileSource
    {
        private readonly string _filePath;

        public MBTilesTileSource(string filePath)
        {
            _filePath = filePath;
        }

        public override Uri GetUri(int zoomLevel, int column, int row) => null;

        public override async Task<ImageSource> LoadImageAsync(int zoomLevel, int column, int row)
        {
            Log.Debug("MBTiles LoadImageAsync called: z={Z} x={X} y={Y} file={File}", zoomLevel, column, row, _filePath);

            if (!File.Exists(_filePath))
                return null;

            var tmsRow = (1 << zoomLevel) - 1 - row;

            // F3: Use SQLiteConnectionStringBuilder to safely set DataSource (avoids semicolons in path breaking the connection string)
            var csb = new SQLiteConnectionStringBuilder { DataSource = _filePath, ReadOnly = true, Version = 3 };
            using (var connection = new SQLiteConnection(csb.ToString()))
            {
                await connection.OpenAsync();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level=@z AND tile_column=@col AND tile_row=@row";
                    cmd.Parameters.AddWithValue("@z", zoomLevel);
                    cmd.Parameters.AddWithValue("@col", column);
                    cmd.Parameters.AddWithValue("@row", tmsRow);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        return null;

                    var data = (byte[])result;
                    // F1+F2: BitmapDecoder.Create does not require a Dispatcher context (safe on ThreadPool threads).
                    // Using block disposes the MemoryStream after OnLoad caches the decoded pixels.
                    using (var ms = new MemoryStream(data))
                    {
                        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var frame = decoder.Frames[0];
                        frame.Freeze();
                        return frame;
                    }
                }
            }
        }
    }
}
