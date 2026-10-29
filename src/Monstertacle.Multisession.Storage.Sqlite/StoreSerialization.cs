using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class StoreSerialization
{
    private static readonly JsonSerializerOptions _options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, _options);
    internal static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, _options)
        ?? throw new JsonException("Stored data is unavailable.");
    internal static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
