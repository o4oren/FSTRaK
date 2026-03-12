using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;

namespace FSTRaK.Utils
{
    /// <summary>
    /// Minimal local HTTP server that serves MBTiles tile data so MapControl can load
    /// tiles via its standard HTTP path, without requiring LoadImageAsync fallback support.
    /// </summary>
    internal static class MBTilesLocalServer
    {
        private static HttpListener _listener;
        private static readonly ConcurrentDictionary<string, string> _filePaths = new ConcurrentDictionary<string, string>();

        public static int Port { get; private set; }

        public static void Start()
        {
            if (_listener != null) return;
            Port = FindFreePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/mbtiles/");
            _listener.Start();
            Task.Run(HandleRequests);
            Log.Debug("MBTiles local server started on port {Port}", Port);
        }

        public static string Register(string filePath)
        {
            var key = Math.Abs(filePath.GetHashCode()).ToString();
            _filePaths[key] = filePath;
            return key;
        }

        private static async Task HandleRequests()
        {
            while (_listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ServeRequest(context));
                }
                catch (Exception ex) when (!(ex is ObjectDisposedException))
                {
                    Log.Error(ex, "MBTiles server error");
                }
            }
        }

        private static void ServeRequest(HttpListenerContext context)
        {
            try
            {
                // URL path: /mbtiles/{key}/{z}/{x}/{y}
                var parts = context.Request.Url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 5 &&
                    _filePaths.TryGetValue(parts[1], out var filePath) &&
                    int.TryParse(parts[2], out int z) &&
                    int.TryParse(parts[3], out int x) &&
                    int.TryParse(parts[4], out int y))
                {
                    var tmsRow = (1 << z) - 1 - y;
                    var csb = new SQLiteConnectionStringBuilder { DataSource = filePath, ReadOnly = true, Version = 3 };
                    using (var connection = new SQLiteConnection(csb.ToString()))
                    {
                        connection.Open();
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level=@z AND tile_column=@x AND tile_row=@tmsRow";
                            cmd.Parameters.AddWithValue("@z", z);
                            cmd.Parameters.AddWithValue("@x", x);
                            cmd.Parameters.AddWithValue("@tmsRow", tmsRow);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                var data = (byte[])result;
                                context.Response.ContentType = "image/png";
                                context.Response.ContentLength64 = data.Length;
                                context.Response.OutputStream.Write(data, 0, data.Length);
                                context.Response.StatusCode = 200;
                            }
                            else
                            {
                                context.Response.StatusCode = 204;
                            }
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MBTiles server request failed");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        private static int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
