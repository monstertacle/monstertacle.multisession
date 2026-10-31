using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks keyset livesight and immutable DTOs using controlled projections.</summary>
public sealed class PageQueryTests
{
    /// <summary>Deleting the last read element does not invalidate continuation; new earlier keys wait for a new scan.</summary>
    [Fact]
    public async Task ContinuesSessionsAcrossDeletionAndInsertionWithoutOffsetDrift()
    {
        InMemorySessionStore store = new();
        SessionSnapshot first = SchedulingData.Session() with { CreatedUtc = DateTimeOffset.UnixEpoch };
        SessionSnapshot later = SchedulingData.Session() with { CreatedUtc = DateTimeOffset.UnixEpoch.AddSeconds(2) };
        await store.CreateSessionAsync(first, CancellationToken.None);
        await store.CreateSessionAsync(later, CancellationToken.None);
        SessionCatalog queries = new(store, [], []);
        Page<SessionSnapshot> page = await queries.ListSessionsAsync(1, null, CancellationToken.None);
        Assert.Equal(first, Assert.Single(page.Items));
        Assert.NotNull(page.NextCursor);
        store.Data.Sessions.Remove(first.SessionId);
        SessionSnapshot before = SchedulingData.Session() with { CreatedUtc = DateTimeOffset.UnixEpoch.AddSeconds(-1) };
        SessionSnapshot between = SchedulingData.Session() with { CreatedUtc = DateTimeOffset.UnixEpoch.AddSeconds(1) };
        await store.CreateSessionAsync(before, CancellationToken.None);
        await store.CreateSessionAsync(between, CancellationToken.None);
        Page<SessionSnapshot> next = await queries.ListSessionsAsync(100, page.NextCursor, CancellationToken.None);
        Assert.Equal(new[] { between.SessionId, later.SessionId }, next.Items.Select(item => item.SessionId));
        Assert.Null(next.NextCursor);
        Assert.Equal(first, Assert.Single(page.Items));
        InMemorySessionStore foreign = new();
        await Assert.ThrowsAsync<SessionOperationException>(() => new SessionCatalog(foreign, [], []).ListSessionsAsync(1, page.NextCursor, CancellationToken.None).AsTask());
    }

    /// <summary>Request cursors are session-bound, ordered by sequence and snapshots do not change with current state.</summary>
    [Fact]
    public async Task OrdersRequestsAndRejectsForeignSessionAndListCursor()
    {
        InMemorySessionStore store = new();
        SessionSnapshot first = SchedulingData.Session();
        SessionSnapshot other = SchedulingData.Session();
        await store.CreateSessionAsync(first, CancellationToken.None);
        await store.CreateSessionAsync(other, CancellationToken.None);
        RequestAdmission requests = new(store, RuntimeOptionsTests.ValidOptions());
        RequestReceipt left = await requests.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "one", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestReceipt right = await requests.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "two", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        Page<RequestSnapshot> page = await requests.ListRequestsAsync(first.SessionId, 1, null, CancellationToken.None);
        Assert.Equal(left.RequestId, Assert.Single(page.Items).RequestId);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(right.RequestId, Assert.Single((await requests.ListRequestsAsync(first.SessionId, 1, page.NextCursor, CancellationToken.None)).Items).RequestId);
        await Assert.ThrowsAsync<SessionOperationException>(() => requests.ListRequestsAsync(other.SessionId, 1, page.NextCursor, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<SessionOperationException>(() => new SessionCatalog(store, [], []).ReadHistoryAsync(first.SessionId, 1, page.NextCursor, CancellationToken.None).AsTask());
        await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.Equal(RequestState.Queued, Assert.Single(page.Items).State);
    }

    /// <summary>Every list facade enforces the same explicit size bounds.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task EnforcesBoundsAcrossFacets(int size)
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        SessionCatalog catalog = new(store, [], []);
        RequestAdmission requests = new(store, RuntimeOptionsTests.ValidOptions());
        Assert.Equal(ErrorCode.InvalidInput, (await Assert.ThrowsAsync<SessionOperationException>(() => catalog.ListSessionsAsync(size, null, CancellationToken.None).AsTask())).Failure.Code);
        Assert.Equal(ErrorCode.InvalidInput, (await Assert.ThrowsAsync<SessionOperationException>(() => requests.ListRequestsAsync(session.SessionId, size, null, CancellationToken.None).AsTask())).Failure.Code);
        Assert.Equal(ErrorCode.InvalidInput, (await Assert.ThrowsAsync<SessionOperationException>(() => catalog.ReadHistoryAsync(session.SessionId, size, null, CancellationToken.None).AsTask())).Failure.Code);
        Assert.Equal(ErrorCode.InvalidInput, (await Assert.ThrowsAsync<SessionOperationException>(() => ((ISessionStore)store).ListRecoveryCasesAsync(size, null, CancellationToken.None).AsTask())).Failure.Code);
    }

    /// <summary>History entries have monotone session-local keys and independently stable UUIDs.</summary>
    [Fact]
    public async Task AssignsHistoryKeysAndPaginatesWithoutChangingOldRecords()
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        await scenario.SubmitAsync("one");
        await scenario.SubmitAsync("two");
        await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None);
        await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None);
        SessionCatalog queries = new(scenario.Store, [], []);
        Page<HistoryEntry> page = await queries.ReadHistoryAsync(scenario.Session.SessionId, 1, null, CancellationToken.None);
        HistoryEntry first = Assert.Single(page.Items);
        Assert.Equal(1, first.HistoryOrdinal);
        Assert.NotEqual(Guid.Empty, first.HistoryId);
        Assert.NotNull(page.NextCursor);
        HistoryEntry second = Assert.Single((await queries.ReadHistoryAsync(scenario.Session.SessionId, 1, page.NextCursor, CancellationToken.None)).Items);
        Assert.Equal(2, second.HistoryOrdinal);
        Assert.NotEqual(first.HistoryId, second.HistoryId);
        Assert.Equal("one", first.Input);
    }
    /// <summary>Creation order uses the host clock and the same snapshot is returned and persisted.</summary>
    [Fact]
    public async Task UsesHostClockForSessionCreationTimestamp()
    {
        ManualTimeProvider clock = new();
        clock.Advance(TimeSpan.FromSeconds(7));
        InMemorySessionStore store = new();
        ControlledProvider provider = new();
        SessionCatalog catalog = new(store, [new("connection", "controlled", 1, provider)],
            [new("profile", "connection", "model", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()))], clock);
        SessionSnapshot created = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        Assert.Equal(clock.GetUtcNow(), created.CreatedUtc);
        Assert.Equal(created, await catalog.GetSessionAsync(created.SessionId, CancellationToken.None));
    }
}
