using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Queries;

/// <summary>Versioned, strict, content-free continuation cursor serialization.</summary>
public static class PageCursor
{
    private static readonly JsonSerializerOptions _options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>Validates an explicit page size without correcting it.</summary>
    public static void ValidateSize(int pageSize)
    {
        if (pageSize is < PageLimits.MinimumSize or > PageLimits.MaximumSize) { throw Invalid(); }
    }

    /// <summary>Encodes the complete last key for a specific list scope.</summary>
    public static string Write(PageScope scope, CursorKey key)
    {
        ValidateScope(scope);
        ValidateKey(key, scope.Kind);
        string cursor = Encode(JsonSerializer.SerializeToUtf8Bytes(new Payload(PageLimits.CursorVersion, scope.StoreId, scope.Kind, scope.SessionId, key), _options));
        if (Encoding.UTF8.GetByteCount(cursor) > PageLimits.MaximumCursorBytes) { throw Invalid(); }
        return cursor;
    }

    /// <summary>Reads a key, or null for the first page; rejects all scope/format errors.</summary>
    public static CursorKey? Read(string? cursor, PageScope scope)
    {
        ValidateScope(scope);
        if (cursor is null) { return null; }
        if (cursor.Length == 0 || Encoding.UTF8.GetByteCount(cursor) > PageLimits.MaximumCursorBytes
            || cursor.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')) { throw Invalid(); }
        try
        {
            string padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
            byte[] bytes = Convert.FromBase64String(padded);
            if (Encode(bytes) != cursor) { throw Invalid(); }
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (!UniqueNames(document.RootElement)) { throw Invalid(); }
            Payload? payload = document.Deserialize<Payload>(_options);
            if (payload is null || payload.Version != PageLimits.CursorVersion || payload.StoreId != scope.StoreId
                || payload.Kind != scope.Kind || payload.SessionId != scope.SessionId || payload.Last is null) { throw Invalid(); }
            ValidateKey(payload.Last, scope.Kind);
            return payload.Last;
        }
        catch (Exception failure) when (failure is FormatException or JsonException or ArgumentException) { throw Invalid(); }
    }

    private static void ValidateScope(PageScope scope)
    {
        bool perSession = scope.Kind is PageListKind.Requests or PageListKind.History;
        if (scope.StoreId == Guid.Empty || !Enum.IsDefined(scope.Kind)
            || (perSession ? scope.SessionId is null || scope.SessionId == Guid.Empty : scope.SessionId is not null)) { throw Invalid(); }
    }

    private static void ValidateKey(CursorKey key, PageListKind kind)
    {
        bool utc = key.Utc is { Offset: var offset } && offset == TimeSpan.Zero;
        bool valid = kind switch
        {
            PageListKind.Sessions => utc && key.Ordinal is null && key.TargetKind is null,
            PageListKind.Requests or PageListKind.History => key.Ordinal > 0 && key.Utc is null && key.TargetKind is null,
            PageListKind.Recovery => utc && key.Ordinal is null && key.TargetKind is { } target && Enum.IsDefined(target),
            _ => false,
        };
        if (key.Id == Guid.Empty || !valid) { throw Invalid(); }
    }

    private static bool UniqueNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            JsonProperty[] properties = element.EnumerateObject().ToArray();
            return properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length
                && properties.All(property => UniqueNames(property.Value));
        }
        return element.ValueKind != JsonValueKind.Array || element.EnumerateArray().All(UniqueNames);
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static SessionOperationException Invalid() => new(new PublicError(ErrorCode.InvalidInput));
    private sealed record Payload(int Version, Guid StoreId, [property: JsonRequired] PageListKind Kind, Guid? SessionId, CursorKey Last);
}
