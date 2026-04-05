using Serilog;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Singleton HTTP tile server. Starts on app launch, stops on app exit.
    /// Listens on http://localhost:{port}/ (default 8765).
    /// </summary>
    internal sealed class TileServer
    {
        private static readonly object Lock = new object();
        private static TileServer _instance;

        public static TileServer Instance
        {
            get
            {
                lock (Lock)
                    return _instance ?? (_instance = new TileServer());
            }
        }

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private readonly TileProxyService _proxy = new TileProxyService();
        private TileHandler _tileHandler;
        private readonly NetworkStateHandler _networkStateHandler = new NetworkStateHandler();
        private readonly NetworkToggleHandler _networkToggleHandler = new NetworkToggleHandler();
        private readonly PanelHandler _panelHandler = new PanelHandler();
        private readonly SimVarHandler _simVarHandler = new SimVarHandler();

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }

        private TileServer() { }

        public void Start()
        {
            if (IsRunning) return;
            Port = FSTRaK.Properties.Settings.Default.TileServerPort;
            _tileHandler = new TileHandler(_proxy);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");

            try
            {
                _listener.Start();
                IsRunning = true;
                Log.Information("TileServer: listening on http://localhost:{Port}/", Port);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Log.Error(ex, "TileServer: failed to start on port {Port}", Port);
                return;
            }

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            IsRunning = false;
            Log.Information("TileServer: stopped.");
        }

        public void ClearTileCache() => _proxy.ClearCache();

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        Log.Warning(ex, "TileServer: listen loop exiting unexpectedly");
                    break;
                }

                // Handle OPTIONS preflight (CORS)
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    try
                    {
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                        context.Response.StatusCode = 204;
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "TileServer: OPTIONS response write failed");
                    }
                    continue;
                }

                _ = Task.Run(() => DispatchAsync(context));
            }
        }

        private async Task DispatchAsync(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath.TrimStart('/');
                var method = context.Request.HttpMethod.ToUpperInvariant();

                if (path.StartsWith("tiles/", StringComparison.OrdinalIgnoreCase))
                {
                    var route = path.Substring("tiles/".Length);
                    await _tileHandler.HandleAsync(context, route);
                }
                else if (path.Equals("network/state", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await _networkStateHandler.HandleAsync(context);
                }
                else if (path.Equals("network/atc/toggle", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await _networkToggleHandler.HandleAsync(context);
                }
                else if (path.Equals("panel", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await _panelHandler.HandleAsync(context);
                }
                else if (path.Equals("simvar", StringComparison.OrdinalIgnoreCase))
                {
                    await _simVarHandler.HandleAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TileServer: unhandled error dispatching request");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.OutputStream.Close();
                }
                catch { }
            }
        }
    }
}
