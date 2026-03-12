using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapControl;
using Serilog;

namespace FSTRaK.Utils
{
    public class MBTilesTileSource : TileSource
    {
        private readonly string _filePath;
        private int _minZoom;
        private Dictionary<int, HashSet<long>> _placeholderTiles;

        public MBTilesTileSource(string filePath)
        {
            _filePath = filePath;
            LoadPlaceholderIndex();
        }

        private void LoadPlaceholderIndex()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var csb = new SQLiteConnectionStringBuilder { DataSource = _filePath, ReadOnly = true, Version = 3 };
                using (var connection = new SQLiteConnection(csb.ToString()))
                {
                    connection.Open();

                    // Read minzoom from metadata
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT value FROM metadata WHERE name = 'minzoom'";
                        var result = cmd.ExecuteScalar() as string;
                        if (result == null || !int.TryParse(result, out _minZoom) || _minZoom <= 0)
                            return;
                    }

                    // Query all tile coordinates at minzoom
                    var minZoomTiles = new List<(int col, int xyzRow)>();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT DISTINCT tile_column, tile_row FROM tiles WHERE zoom_level = @z";
                        cmd.Parameters.AddWithValue("@z", _minZoom);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var col = reader.GetInt32(0);
                                var tmsRow = reader.GetInt32(1);
                                var xyzRow = (1 << _minZoom) - 1 - tmsRow;
                                minZoomTiles.Add((col, xyzRow));
                            }
                        }
                    }

                    if (minZoomTiles.Count == 0)
                        return;

                    // Only generate placeholders for zoom levels close to minzoom (within 3 levels).
                    // At very low zoom levels, a single tile covers continents — useless as an indicator.
                    _placeholderTiles = new Dictionary<int, HashSet<long>>();
                    var lowestPlaceholderZoom = Math.Max(0, _minZoom - 3);
                    for (var z = lowestPlaceholderZoom; z < _minZoom; z++)
                    {
                        var diff = _minZoom - z;
                        var set = new HashSet<long>();
                        foreach (var (col, xyzRow) in minZoomTiles)
                        {
                            var parentCol = col >> diff;
                            var parentRow = xyzRow >> diff;
                            set.Add(((long)parentCol << 32) | (uint)parentRow);
                        }
                        _placeholderTiles[z] = set;
                    }

                    Log.Debug("MBTiles placeholder index built: minZoom={MinZoom}, {Count} tiles at minzoom",
                        _minZoom, minZoomTiles.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load MBTiles placeholder index from {Path}", _filePath);
            }
        }

        public override Uri GetUri(int column, int row, int zoomLevel) => null;

        public override async Task<ImageSource> LoadImageAsync(int column, int row, int zoomLevel)
        {
            if (!File.Exists(_filePath))
                return null;

            var tmsRow = (1 << zoomLevel) - 1 - row;

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
                    if (result != null && result != DBNull.Value)
                    {
                        var data = (byte[])result;
                        using (var ms = new MemoryStream(data))
                        {
                            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            var frame = decoder.Frames[0];
                            frame.Freeze();
                            return frame;
                        }
                    }

                    // No tile data — if below min zoom, check if this tile is a parent of actual tiles
                    if (_placeholderTiles != null &&
                        _placeholderTiles.TryGetValue(zoomLevel, out var tiles) &&
                        tiles.Contains(((long)column << 32) | (uint)row))
                    {
                        return CreatePlaceholderTile();
                    }

                    return null;
                }
            }
        }

        private static ImageSource CreatePlaceholderTile()
        {
            const int size = 256;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(80, 70, 130, 180)),
                    new Pen(new SolidColorBrush(Color.FromArgb(120, 70, 130, 180)), 1),
                    new Rect(0, 0, size, size));
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
    }
}
