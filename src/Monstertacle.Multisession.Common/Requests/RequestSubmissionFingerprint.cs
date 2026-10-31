using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Canonical exact identity of immutable submitted input, independent from a returned receipt.</summary>
public static class RequestSubmissionFingerprint
{
    private const int _encodingVersion = 1;

    /// <summary>Computes the versioned identity of all immutable submission fields without text normalization.</summary>
    public static string Compute(SubmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArrayBufferWriter<byte> bytes = new();
        using Utf8JsonWriter writer = new(bytes);
        writer.WriteStartArray();
        writer.WriteNumberValue(_encodingVersion);
        writer.WriteStringValue(request.SessionId);
        writer.WriteStringValue(request.Input);
        writer.WriteNumberValue(request.Timeout.Ticks);
        writer.WriteStringValue(request.WorkspaceKey);
        writer.WriteBooleanValue(request.WriteIntent);
        writer.WriteEndArray();
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(bytes.WrittenSpan));
    }
}
