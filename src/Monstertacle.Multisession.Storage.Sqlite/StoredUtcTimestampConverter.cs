using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monstertacle.Multisession.Storage.Sqlite;

// New snapshots use UTC consistently with indexed columns. Reads preserve evidence of old inconsistent offsets.
internal sealed class StoredUtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}
