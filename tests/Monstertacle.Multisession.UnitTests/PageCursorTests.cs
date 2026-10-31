using System.Text;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks strict cursor decoding and shared page bounds without a database.</summary>
public sealed class PageCursorTests
{
    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Bounds are inclusive and invalid sizes are never silently capped.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ValidatesInclusivePageBounds(int size, bool valid)
    {
        if (valid) { PageCursor.ValidateSize(size); }
        else { Assert.Equal(ErrorCode.InvalidInput, Assert.Throws<SessionOperationException>(() => PageCursor.ValidateSize(size)).Failure.Code); }
    }

    /// <summary>Every list has a complete stable key and its own exact scope.</summary>
    [Theory]
    [InlineData(PageListKind.Sessions)]
    [InlineData(PageListKind.Requests)]
    [InlineData(PageListKind.History)]
    [InlineData(PageListKind.Recovery)]
    public void RoundTripsCompleteKeys(PageListKind kind)
    {
        PageScope scope = new(Guid.NewGuid(), kind, kind is PageListKind.Requests or PageListKind.History ? Guid.NewGuid() : null);
        CursorKey key = new(Guid.NewGuid(), kind is PageListKind.Requests or PageListKind.History ? 7 : null,
            kind is PageListKind.Sessions or PageListKind.Recovery ? DateTimeOffset.UnixEpoch : null,
            kind == PageListKind.Recovery ? RecoveryTargetKind.RequestExecution : null);
        Assert.Null(PageCursor.Read(null, scope));
        string cursor = PageCursor.Write(scope, key);
        Assert.NotEmpty(cursor);
        Assert.Equal(key, PageCursor.Read(cursor, scope));
        Assert.Equal(ErrorCode.InvalidInput, Assert.Throws<SessionOperationException>(() => PageCursor.Read(cursor, scope with { StoreId = Guid.NewGuid() })).Failure.Code);
    }

    /// <summary>Empty, malformed, padded and oversized encodings are invalid.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not+base64/url")]
    [InlineData("e30=")]
    [InlineData("a")]
    [InlineData("____")]
    public void RejectsMalformedRawCursor(string cursor)
    {
        PageScope scope = new(Guid.NewGuid(), PageListKind.Sessions);
        Assert.Equal(ErrorCode.InvalidInput, Assert.Throws<SessionOperationException>(() => PageCursor.Read(cursor, scope)).Failure.Code);
        Assert.Equal(ErrorCode.InvalidInput, Assert.Throws<SessionOperationException>(() => PageCursor.Read(new string('a', 2049), scope)).Failure.Code);
    }

    /// <summary>JSON parsing rejects incompatible versions, keys, fields and scope even when Base64url is well formed.</summary>
    [Theory]
    [InlineData("version")]
    [InlineData("store")]
    [InlineData("list")]
    [InlineData("missing-key")]
    [InlineData("missing-kind")]
    [InlineData("empty-id")]
    [InlineData("non-utc")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    public void RejectsInvalidDecodedCursor(string invalid)
    {
        PageScope scope = new(Guid.NewGuid(), PageListKind.Sessions);
        JsonObject key = new() { ["Id"] = Guid.NewGuid().ToString("D"), ["Ordinal"] = null, ["Utc"] = "1970-01-01T00:00:00+00:00", ["TargetKind"] = null };
        JsonObject json = new() { ["Version"] = 1, ["StoreId"] = scope.StoreId.ToString("D"), ["Kind"] = (int)scope.Kind, ["SessionId"] = null, ["Last"] = key };
        switch (invalid)
        {
            case "version": json["Version"] = 2; break;
            case "store": json["StoreId"] = Guid.NewGuid().ToString("D"); break;
            case "list": json["Kind"] = (int)PageListKind.Requests; break;
            case "missing-key": json.Remove("Last"); break;
            case "missing-kind": json.Remove("Kind"); break;
            case "empty-id": key["Id"] = Guid.Empty.ToString("D"); break;
            case "non-utc": key["Utc"] = "1970-01-01T01:00:00+01:00"; break;
            case "unknown-field": json["Unexpected"] = true; break;
        }
        string raw = json.ToJsonString();
        if (invalid == "duplicate-field") { raw = raw.Insert(1, "\"Version\":1,"); }
        Assert.Equal(ErrorCode.InvalidInput, Assert.Throws<SessionOperationException>(() => PageCursor.Read(Encode(raw), scope)).Failure.Code);
    }
}
