using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Culture and timezone representations cannot change stored instants, opaque identifiers or text.</summary>
public sealed class SerializationCultureTests
{
    /// <summary>UTC snapshots, Unicode content and cursors survive a culture change and actual SQLite reopening.</summary>
    [Theory]
    [InlineData("de-DE", "Europe/Berlin", 1)]
    [InlineData("de-DE", "Europe/Berlin", 7)]
    [InlineData("en-US", "America/New_York", 1)]
    [InlineData("en-US", "America/New_York", 7)]
    [InlineData("tr-TR", "Europe/Istanbul", 1)]
    [InlineData("tr-TR", "Europe/Istanbul", 7)]
    public async Task RoundTripsUtcAndOpaqueDataAcrossCultures(string culture, string zoneId, int month)
    {
        using CultureScope writingCulture = new(culture);
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        DateTimeOffset utc = new(2026, month, 1, 12, 30, 0, TimeSpan.Zero);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, zone);
        Assert.NotEqual(TimeSpan.Zero, local.Offset);
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        string storeId = (string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT StoreId FROM Metadata")!;
        SessionSnapshot first = await CreateAsync(fixture.Store, local);
        Assert.Equal(TimeSpan.Zero, first.CreatedUtc.Offset);
        Assert.Equal(utc, first.CreatedUtc);
        await CreateAsync(fixture.Store, local);
        const string input = " I/i/İ/ı\n🙂\r\n ";
        const string output = " complete I/i/İ/ı🙂\r\n ";
        RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(first.SessionId, input, TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
        ExecutionReservation reservation = (await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None))!;
        await fixture.Store.CommitCompletionAsync(reservation.DispatchId, new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded,
            output, true, first.Binding! with { ContinuationState = "head-I-İ-ı-i" }, null), CancellationToken.None);
        Page<SessionSnapshot> page = await fixture.Store.ListSessionsAsync(1, null, CancellationToken.None);
        Assert.NotNull(page.NextCursor);
        using CultureScope readingCulture = new(culture == "tr-TR" ? "de-DE" : "tr-TR");
        await fixture.ReopenAsync();
        SessionSnapshot reopened = (await fixture.Store.GetSessionAsync(first.SessionId, CancellationToken.None))!;
        Assert.Equal(TimeSpan.Zero, reopened.CreatedUtc.Offset);
        Assert.Equal(utc, reopened.CreatedUtc);
        Assert.Equal("model-I-İ-ı-i", reopened.Context.ModelId);
        Assert.Equal("upper", reopened.Context.Settings["I"]);
        Assert.Equal("lower", reopened.Context.Settings["i"]);
        Assert.Equal("31.12.2026", reopened.Context.Settings["date-like"]);
        Assert.Equal("head-I-İ-ı-i", reopened.Binding!.ContinuationState);
        RequestSnapshot request = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(TimeSpan.Zero, request.AcceptedUtc.Offset);
        Assert.Equal(TimeSpan.Zero, request.EndedUtc!.Value.Offset);
        HistoryEntry history = Assert.Single((await fixture.Store.ReadHistoryAsync(first.SessionId, 10, null, CancellationToken.None)).Items);
        Assert.Equal(input, history.Input);
        Assert.Equal(output, history.Result.Output);
        Page<SessionSnapshot> next = await fixture.Store.ListSessionsAsync(1, page.NextCursor, CancellationToken.None);
        Assert.Single(next.Items);
        Assert.NotEqual(page.Items[0].SessionId, next.Items[0].SessionId);
        Assert.Null(next.NextCursor);
        Assert.Equal(storeId, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT StoreId FROM Metadata"));
        Assert.Equal(fixture.Directory.TrustScopeId.ToString("D"), StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT InstanceTrustScopeId FROM Metadata"));
        string created = (string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT CreatedUtc FROM Metadata")!;
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.ParseExact(created, "O", CultureInfo.InvariantCulture).Offset);
    }

    private static async Task<SessionSnapshot> CreateAsync(ISessionStore store, DateTimeOffset local)
    {
        Guid identity = Guid.NewGuid();
        ProviderSessionContext context = new(identity, "model-I-İ-ı-i", ExecutionKind.ModelResponse, new(null, "workspace-I-İ"),
            new Dictionary<string, string> { ["I"] = "upper", ["i"] = "lower", ["date-like"] = "31.12.2026" });
        SessionSnapshot snapshot = new(identity, "provider-I-İ", "profile-I-İ", context, null)
        { CreatedUtc = local, UpdatedUtc = local, SettingsRevision = "revision-I-İ" };
        await store.CreateSessionAsync(snapshot, CancellationToken.None);
        return await store.CommitSessionBindingAsync(identity, new("fixture-adapter", 7, "native-I-İ-" + identity.ToString("D"), null), CancellationToken.None);
    }
}
