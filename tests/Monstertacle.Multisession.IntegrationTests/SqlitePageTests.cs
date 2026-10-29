using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Tests native bounded keysets using only actual persisted rows and public facades.</summary>
public sealed class SqlitePageTests
{
    private static async Task SeedSessionAsync(AdmissionStoreFixture fixture, Guid id)
    {
        ProviderSessionContext context = new(id, "model", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
        SessionSnapshot session = new(id, "connection", "profile", context, null)
        {
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
            SettingsRevision = "revision",
        };
        await fixture.Store.CreateSessionAsync(session, CancellationToken.None);
        await fixture.Store.CommitSessionBindingAsync(id, new("fixture-adapter", 7, "native-" + id.ToString("D"), null), CancellationToken.None);
    }

    /// <summary>Tied creation times use ordinal UUIDs and continuation survives reopening and intervening insertion.</summary>
    [Fact]
    public async Task PagesSessionsByCompleteStableKeyAcrossReopen()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        Guid first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid third = Guid.Parse("30000000-0000-0000-0000-000000000001");
        await SeedSessionAsync(fixture, third);
        await SeedSessionAsync(fixture, first);
        await SeedSessionAsync(fixture, second);
        Page<SessionSnapshot> page = await fixture.Catalog.ListSessionsAsync(2, null, CancellationToken.None);
        Assert.Equal(new[] { first, second }, page.Items.Select(item => item.SessionId));
        Assert.NotNull(page.NextCursor);
        await SeedSessionAsync(fixture, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Guid after = Guid.Parse("40000000-0000-0000-0000-000000000001");
        await SeedSessionAsync(fixture, after);
        await fixture.ReopenAsync();
        Page<SessionSnapshot> continuation = await fixture.Catalog.ListSessionsAsync(2, page.NextCursor, CancellationToken.None);
        Assert.Equal(new[] { third, after }, continuation.Items.Select(item => item.SessionId));
        Assert.Null(continuation.NextCursor);
    }

    /// <summary>Original candidates are discoverable in accepted sequence without retaining a receipt.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public async Task PagesAcceptedCandidatesInStoredSequence(int size)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        List<Guid> expected = [];
        for (int index = 0; index < 5; index++)
        {
            Guid candidate = Guid.NewGuid();
            expected.Add(candidate);
            await fixture.Store.CommitAcceptanceAsync(new(candidate, new(session.SessionId, "new input " + index, TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        }
        await fixture.ReopenAsync();
        RequestQueries queries = new(fixture.Store);
        List<RequestSnapshot> rows = [];
        string? cursor = null;
        do
        {
            Page<RequestSnapshot> page = await queries.ListRequestsAsync(session.SessionId, size, cursor, CancellationToken.None);
            Assert.InRange(page.Items.Count, 1, size);
            rows.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(expected, rows.Select(item => item.RequestId));
        Assert.Equal(Enumerable.Range(1, 5).Select(value => (long)value), rows.Select(item => item.Sequence));
        Assert.All(rows, item => Assert.Equal(RequestState.Queued, item.State));
    }

    /// <summary>A cursor is scoped to its original store, list and session.</summary>
    [Theory]
    [InlineData("store")]
    [InlineData("session")]
    [InlineData("kind")]
    public async Task RejectsForeignCursorScope(string mismatch)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        await using AdmissionStoreFixture other = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        for (int index = 0; index < 2; index++)
        {
            await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        }
        Page<RequestSnapshot> page = await fixture.Store.ListRequestsAsync(session.SessionId, 1, null, CancellationToken.None);
        Assert.NotNull(page.NextCursor);
        ISessionStore target = mismatch == "store" ? other.Store : fixture.Store;
        Guid sessionId = mismatch == "session" ? Guid.NewGuid() : session.SessionId;
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => mismatch == "kind"
            ? target.ListSessionsAsync(1, page.NextCursor, CancellationToken.None).AsTask()
            : (Task)target.ListRequestsAsync(sessionId, 1, page.NextCursor, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
    }

    /// <summary>Direct native reads enforce bounded sizes and the existing strict cursor parser.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(101, null)]
    [InlineData(1, "not+a/cursor=")]
    public async Task RejectsInvalidPageArguments(int size, string? cursor)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Store.ListSessionsAsync(size, cursor, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
    }

    /// <summary>A foreign session identity has no visible requests and no continuation.</summary>
    [Fact]
    public async Task ReturnsNoRowsForUnknownSession()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        Page<RequestSnapshot> page = await fixture.Store.ListRequestsAsync(Guid.NewGuid(), 100, null, CancellationToken.None);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }
}
