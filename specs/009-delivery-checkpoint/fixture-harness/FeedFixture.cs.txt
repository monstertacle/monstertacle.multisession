using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Monstertacle.Multisession.Delivery.FixtureHarness;

/// <summary>Answers one framed notifications request with the stored events after the requested cursor.</summary>
internal static class FeedFixture
{
    private const int MaximumFrameBytes = 262144;

    public static async Task<int> ServeAsync(string eventsPath)
    {
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        var request = JsonNode.Parse(await ReadFrameAsync(input))!;
        var cursor = request["cursor"]!.GetValue<long>();
        var events = JsonNode.Parse(await File.ReadAllBytesAsync(eventsPath))!.AsArray();
        var page = new JsonArray();
        foreach (var item in events)
        {
            if (item!["cursor"]!.GetValue<long>() > cursor)
            {
                page.Add(item.DeepClone());
            }
        }
        var response = new JsonObject
        {
            ["protocolVersion"] = 1,
            ["requestId"] = request["requestId"]!.GetValue<string>(),
            ["ok"] = true,
            ["code"] = "ok",
            ["observedUtc"] = "2026-11-01T00:00:00+00:00",
            ["data"] = page,
        };
        await WriteFrameAsync(output, JsonSerializer.SerializeToUtf8Bytes(response));
        return 0;
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream)
    {
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix);
        var size = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (size < 1 || size > MaximumFrameBytes)
        {
            throw new InvalidDataException("feed_fixture_frame");
        }
        var payload = new byte[size];
        await stream.ReadExactlyAsync(payload);
        return payload;
    }

    private static async Task WriteFrameAsync(Stream stream, byte[] payload)
    {
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }
}
