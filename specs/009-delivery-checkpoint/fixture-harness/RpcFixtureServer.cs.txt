using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Monstertacle.Multisession.Delivery.FixtureHarness;

/// <summary>
/// Minimal private Unix-socket WebSocket RPC destination. The mode file selects the reply:
/// idle (ready, accepts turns), unready (thread not ready), lose (closes after a turn start) or accept.
/// </summary>
internal static class RpcFixtureServer
{
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaximumHeaderBytes = 16384;
    private const int MaximumMessageBytes = 1048576;
    private const UnixFileMode PrivateSocket = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static async Task<int> RunAsync(string socketPath, string modePath, string logPath)
    {
        File.Delete(socketPath);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        File.SetUnixFileMode(socketPath, PrivateSocket);
        listener.Listen();
        while (true)
        {
            var client = await listener.AcceptAsync();
            _ = Task.Run(() => ServeAsync(client, modePath, logPath));
        }
    }

    private static async Task ServeAsync(Socket client, string modePath, string logPath)
    {
        await using var stream = new NetworkStream(client, ownsSocket: true);
        try
        {
            await HandshakeAsync(stream);
            using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });
            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveAsync(socket);
                if (message is null)
                {
                    return;
                }
                if (!await AnswerAsync(socket, message, modePath, logPath))
                {
                    socket.Abort();
                    return;
                }
            }
        }
        catch (Exception e) when (e is IOException or WebSocketException or InvalidDataException)
        {
            await Console.Error.WriteLineAsync("rpc_fixture_connection:" + e.GetType().Name);
        }
    }

    private static async Task<bool> AnswerAsync(WebSocket socket, JsonNode message, string modePath, string logPath)
    {
        var method = message["method"]!.GetValue<string>();
        if (message["id"] is not { } id)
        {
            return true;
        }
        var mode = (await File.ReadAllTextAsync(modePath)).Trim();
        JsonNode result;
        switch (method)
        {
            case "initialize":
                result = new JsonObject { ["userAgent"] = "fixture" };
                break;
            case "thread/read":
                var status = mode == "unready" ? "systemError" : "idle";
                var threadId = message["params"]!["threadId"]!.GetValue<string>();
                result = new JsonObject { ["thread"] = new JsonObject { ["id"] = threadId, ["status"] = new JsonObject { ["type"] = status } } };
                break;
            case "turn/start":
                await File.AppendAllTextAsync(logPath, message["params"]!["threadId"]!.GetValue<string>() + " " + mode + "\n");
                if (mode == "lose")
                {
                    return false;
                }
                result = new JsonObject { ["turn"] = new JsonObject { ["id"] = "fixture-turn-" + Guid.NewGuid().ToString("N") } };
                break;
            default:
                throw new InvalidDataException("rpc_fixture_method");
        }
        var reply = new JsonObject { ["id"] = id.DeepClone(), ["result"] = result };
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(reply), WebSocketMessageType.Text, true, CancellationToken.None);
        return true;
    }

    private static async Task<JsonNode?> ReceiveAsync(WebSocket socket)
    {
        using var message = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (part.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (message.Length + part.Count > MaximumMessageBytes)
            {
                throw new InvalidDataException("rpc_fixture_message");
            }
            message.Write(buffer, 0, part.Count);
        }
        while (!part.EndOfMessage);
        return JsonNode.Parse(message.ToArray());
    }

    private static async Task HandshakeAsync(NetworkStream stream)
    {
        var header = new List<byte>();
        var one = new byte[1];
        while (!EndsWithBlankLine(header))
        {
            if (await stream.ReadAsync(one) != 1 || header.Count > MaximumHeaderBytes)
            {
                throw new InvalidDataException("rpc_fixture_handshake");
            }
            header.Add(one[0]);
        }
        var key = Encoding.ASCII.GetString([.. header]).Split("\r\n")
            .Select(line => line.Split(':', 2))
            .Single(parts => parts.Length == 2 && parts[0].Trim().Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))[1].Trim();
#pragma warning disable CA5350 // RFC 6455 fixes SHA-1 for the WebSocket handshake; it protects no secret.
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
#pragma warning restore CA5350
        var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }

    private static bool EndsWithBlankLine(List<byte> header) =>
        header.Count >= 4 && header[^4] == '\r' && header[^3] == '\n' && header[^2] == '\r' && header[^1] == '\n';
}
