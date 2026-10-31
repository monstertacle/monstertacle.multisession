using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks frozen settings and timestamps of committed local lifecycle mutations.</summary>
public sealed class SessionLifecycleTests
{
    private static SessionCatalog Catalog(InMemorySessionStore store, ControlledProvider provider, TimeProvider clock)
        => new(store, [new("connection", "controlled", 1, provider)],
            [new("profile", "connection", "model", ExecutionKind.AgentTask, new("immutable-revision", new Dictionary<string, string> { ["instruction"] = "frozen" }))], clock);

    /// <summary>Opening is observable before external I/O and the return value equals the committed snapshot.</summary>
    [Fact]
    public async Task PreservesRevisionAndReturnsCommittedOpeningTimestamps()
    {
        ManualTimeProvider clock = new();
        clock.Advance(TimeSpan.FromDays(1));
        InMemorySessionStore store = new() { Clock = clock };
        SessionSnapshot? opening = null;
        ControlledProvider provider = new()
        {
            Opening = context => { opening = store.FindSession(context.SessionId); clock.Advance(TimeSpan.FromSeconds(2)); },
        };
        SessionCatalog catalog = Catalog(store, provider, clock);
        SessionSnapshot created = await catalog.CreateSessionAsync("profile", new(null, null), CancellationToken.None);
        Assert.NotNull(opening);
        Assert.Equal("immutable-revision", opening.SettingsRevision);
        Assert.Equal("frozen", opening.Context.Settings["instruction"]);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(1), opening.CreatedUtc);
        Assert.Equal(opening.CreatedUtc, opening.UpdatedUtc);
        Assert.Null(opening.ClosedUtc);
        Assert.Equal("immutable-revision", created.SettingsRevision);
        Assert.Equal(opening.CreatedUtc, created.CreatedUtc);
        Assert.Equal(clock.GetUtcNow(), created.UpdatedUtc);
        Assert.Null(created.ClosedUtc);
        Assert.Equal(created, await catalog.GetSessionAsync(created.SessionId, CancellationToken.None));
    }

    /// <summary>A failed native open updates the local record without assigning a closure time.</summary>
    [Fact]
    public async Task StampsUncertainOpeningWithoutChangingCreationOrRevision()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        ControlledProvider provider = new() { Opening = _ => { clock.Advance(TimeSpan.FromSeconds(3)); throw new IOException("private"); } };
        SessionCatalog catalog = Catalog(store, provider, clock);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            catalog.CreateSessionAsync("profile", new(null, null), CancellationToken.None).AsTask());
        SessionSnapshot? saved = await catalog.GetSessionAsync(failure.Failure.SessionId!.Value, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("immutable-revision", saved.SettingsRevision);
        Assert.Equal(DateTimeOffset.UnixEpoch, saved.CreatedUtc);
        Assert.Equal(clock.GetUtcNow(), saved.UpdatedUtc);
        Assert.Equal(TimeSpan.Zero, saved.UpdatedUtc.Offset);
        Assert.Equal(SessionBlockReason.RecoveryRequired, saved.BlockReason);
        Assert.Null(saved.ClosedUtc);
    }

    /// <summary>Confirmed and uncertain execution change update time while preserving immutable metadata.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StampsExecutionFinalization(bool uncertain)
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        SessionSnapshot session = SchedulingData.Session() with { SettingsRevision = "original", CreatedUtc = clock.GetUtcNow(), UpdatedUtc = clock.GetUtcNow() };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(session.SessionId, "new input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation reservation = (await store.TryReserveHeadAsync(4, CancellationToken.None))!;
        clock.Advance(TimeSpan.FromSeconds(4));
        if (uncertain) { await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None); }
        else { await store.CommitCompletionAsync(reservation.DispatchId, DispatchScenario.Success(session.Binding!), CancellationToken.None); }
        SessionSnapshot saved = store.FindSession(session.SessionId)!;
        Assert.Equal(session.SettingsRevision, saved.SettingsRevision);
        Assert.Equal(session.CreatedUtc, saved.CreatedUtc);
        Assert.Equal(clock.GetUtcNow(), saved.UpdatedUtc);
        Assert.Null(saved.ClosedUtc);
    }

    /// <summary>Close intent has no closure timestamp; only confirmed end can stamp actual closure.</summary>
    [Fact]
    public async Task StampsRecoveryClosureOnlyAfterConfirmedEnd()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening, Binding = null, SettingsRevision = "original" };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        RecoveryTarget target = new(RecoveryTargetKind.SessionOpening, session.SessionId);
        RecoveryCaseSnapshot item = (await ((ISessionStore)store).GetRecoveryCaseAsync(target, CancellationToken.None))!;
        clock.Advance(TimeSpan.FromSeconds(1));
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), target, item.Revision, RecoveryDecisionKind.CloseSession, "operator", "evidence"), null, CancellationToken.None);
        SessionSnapshot closing = store.FindSession(session.SessionId)!;
        Assert.Equal(clock.GetUtcNow(), closing.UpdatedUtc);
        Assert.Null(closing.ClosedUtc);
        item = (await ((ISessionStore)store).GetRecoveryCaseAsync(target, CancellationToken.None))!;
        clock.Advance(TimeSpan.FromSeconds(1));
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), target, item.Revision, RecoveryDecisionKind.ConfirmEnded, "operator", "evidence"), null, CancellationToken.None);
        SessionSnapshot closed = store.FindSession(session.SessionId)!;
        Assert.Equal(SessionLifecycle.Closed, closed.Lifecycle);
        Assert.Equal(clock.GetUtcNow(), closed.ClosedUtc);
        Assert.Equal(closed.ClosedUtc, closed.UpdatedUtc);
        Assert.Equal("original", closed.SettingsRevision);
    }

    /// <summary>A second startup blocks a stranded opening using the actual recovery observation time.</summary>
    [Fact]
    public async Task StampsBootstrapRecovery()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening };
        await store.CreateSessionAsync(session, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        await ((IStoreBootstrap)store).StartAsync(new TestStateDirectoryLease(Path.GetTempPath()), CancellationToken.None);
        Assert.Equal(clock.GetUtcNow(), store.FindSession(session.SessionId)?.UpdatedUtc);
    }
}
