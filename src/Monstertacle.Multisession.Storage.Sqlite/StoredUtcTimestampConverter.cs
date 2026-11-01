using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Snapshots store UTC consistently with indexed columns; the snapshot consistency checks refuse any stored form that differs.
internal sealed class StoredUtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}
