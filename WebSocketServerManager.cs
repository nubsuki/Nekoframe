using Fleck;
using Nekoframe.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Nekoframe;


// Manages the Fleck WebSocket server.

public class WebSocketServerManager : IDisposable
{
    private readonly WebSocketServer _server;
    private readonly List<IWebSocketConnection> _clients = new();
    private readonly object _lock = new();
    private System.Threading.Timer? _broadcastTimer;
    private bool _disposed;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Include,
    };

    public WebSocketServerManager(string address = "ws://0.0.0.0:8181")
    {
        // Suppress Fleck's default verbose console logging
        FleckLog.LogAction = (level, message, ex) =>
        {
            if (level >= LogLevel.Warn)
                Console.WriteLine($"[Fleck/{level}] {message} {ex?.Message}");
        };

        _server = new WebSocketServer(address);
    }


    // Starts accepting WebSocket connections and begins the broadcast loop.

    public void Start(StatsFetcher fetcher, int broadcastIntervalMs = 1000)
    {
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                lock (_lock) _clients.Add(socket);
                Console.WriteLine($"[WS] Client connected: {socket.ConnectionInfo.ClientIpAddress}");
            };

            socket.OnClose = () =>
            {
                lock (_lock) _clients.Remove(socket);
                Console.WriteLine($"[WS] Client disconnected.");
            };

            socket.OnError = ex =>
            {
                Console.WriteLine($"[WS] Error: {ex.Message}");
                lock (_lock) _clients.Remove(socket);
            };

            socket.OnMessage = msg =>
            {
                // No inbound commands needed right now — reserved for future use
                Console.WriteLine($"[WS] Received from client: {msg}");
            };
        });

        _broadcastTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                var stats = fetcher.GetStats();
                var json = JsonConvert.SerializeObject(stats, JsonSettings);
                BroadcastAll(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stats] Error collecting stats: {ex.Message}");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(broadcastIntervalMs));

        Console.WriteLine($"[WS] WebSocket server running on ws://localhost:8181");
    }

    private void BroadcastAll(string message)
    {
        List<IWebSocketConnection> snapshot;
        lock (_lock) snapshot = new List<IWebSocketConnection>(_clients);

        foreach (var client in snapshot)
        {
            try
            {
                if (client.IsAvailable)
                    client.Send(message);
            }
            catch
            {
                lock (_lock) _clients.Remove(client);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _broadcastTimer?.Dispose();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
