using System;
using System.Data.SQLite;
using System.Globalization;
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
        private int _maxZoom;
        private double _boundsWest;
        private double _boundsEast;
        private double _boundsSouth;
        private double _boundsNorth;
        private bool _metadataLoaded;

        public MBTilesTileSource(string filePath)
        {
            _filePath = filePath;
            LoadMetadata();
        }

        private void LoadMetadata()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var csb = new SQLiteConnectionStringBuilder { DataSource = _filePath, ReadOnly = true, Version = 3 };
                using (var connection = new SQLiteConnection(csb.ToString()))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name, value FROM metadata WHERE name IN ('minzoom','maxzoom','bounds')";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var name = reader.GetString(0);
                                var value = reader.GetString(1);
                                switch (name)
                                {
                                    case "minzoom":
                                        int.TryParse(value, out _minZoom);
                                        break;
                                    case "maxzoom":
                                        int.TryParse(value, out _maxZoom);
                                        break;
                                    case "bounds":
                                        var parts = value.Split(',');
                                        if (parts.Length == 4)
                                        {
                                            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _boundsWest);
                                            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _boundsSouth);
                                            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _boundsEast);
                                            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out _boundsNorth);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
                _metadataLoaded = _minZoom > 0 && _boundsWest != 0;
                Log.Debug("MBTiles metadata: minZoom={MinZoom}, maxZoom={MaxZoom}, bounds=({W},{S},{E},{N})",
                    _minZoom, _maxZoom, _boundsWest, _boundsSouth, _boundsEast, _boundsNorth);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load MBTiles metadata from {Path}", _filePath);
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

                    // No tile data — if below min zoom and tile overlaps bounds, show placeholder
                    if (_metadataLoaded && zoomLevel < _minZoom && TileOverlapsBounds(column, row, zoomLevel))
                    {
                        return CreatePlaceholderTile();
                    }

                    return null;
                }
            }
        }

        private bool TileOverlapsBounds(int column, int row, int zoomLevel)
        {
            var n = 1 << zoomLevel;
            var tileWest = column * 360.0 / n - 180.0;
            var tileEast = (column + 1) * 360.0 / n - 180.0;
            // Row 0 is top (north) in XYZ/slippy convention
            var tileNorth = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * row / n))) * 180.0 / Math.PI;
            var tileSouth = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * (row + 1) / n))) * 180.0 / Math.PI;

            return tileEast > _boundsWest && tileWest < _boundsEast &&
                   tileNorth > _boundsSouth && tileSouth < _boundsNorth;
        }

        private static ImageSource CreatePlaceholderTile()
        {
            const int size = 256;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Semi-transparent light blue fill
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
