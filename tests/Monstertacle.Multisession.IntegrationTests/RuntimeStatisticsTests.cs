using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Authoritative diagnostics retain unknown occupancy and exclude released reservations.</summary>
public sealed class RuntimeStatisticsTests
{
    /// <summary>A real reopened runtime exports its durable unknown occupancy without allowing a queued follow-up send.</summary>
    [Fact]
    public async Task ReopenedRuntimePublishesAuthoritativeCachedGauges()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(fixture.Session.SessionId, "waiting", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        RetainedTestLease lease = new(fixture.Host.Directory);
        ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, fixture.Host.Directory.TrustScopeId, fixture.Host.Options, CancellationToken.None);
        using SessionDiagnostics diagnostics = new();
        using MetricGaugeCapture metrics = new(diagnostics.InstanceId);
        await using SessionRuntime runtime = new(store, lease,
            [new("connection-I", "fixture-adapter", 7, fixture.Host.Provider)], [], diagnostics: diagnostics);
        await runtime.StartAsync(fixture.Host.Options, CancellationToken.None);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (metrics.Read().Count == 0) { await Task.Delay(10, deadline.Token); }
        Assert.Equal(1, metrics.Read()["multisession.requests.queued"]);
        Assert.Equal(1, metrics.Read()["multisession.reservations.occupied"]);
        Assert.Equal(1, metrics.Read()["multisession.reservations.uncertain"]);
        Assert.Equal(ExecutionCertainty.Unknown, (await runtime.Requests.GetRequestAsync(fixture.Receipt.RequestId, deadline.Token))!.Certainty);
        await runtime.StopAsync(TimeSpan.FromSeconds(5), deadline.Token);
        Assert.Empty(metrics.Read());
    }

    /// <summary>Each read is a consistent projection of the current durable queue and reservation graph.</summary>
    [Fact]
    public async Task CountsQueuedOccupiedAndUncertainAcrossRestartAndResolution()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        Assert.Equal(new(0, 1, 0), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
        await store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(fixture.Session.SessionId, "waiting", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        Assert.Equal(new(1, 1, 0), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        Assert.Equal(new(1, 1, 1), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
        await fixture.Host.ReopenAsync();
        store = fixture.Host.Store;
        Assert.Equal(new(1, 1, 1), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "confirmed end", "non-secret evidence"), fixture.Session.Binding, CancellationToken.None);
        Assert.Equal(new(1, 0, 0), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
        await store.CancelQueuedSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        Assert.Equal(new(0, 0, 0), await store.GetRuntimeStatisticsAsync(CancellationToken.None));
    }

    /// <summary>Cancelled reads never enqueue an unbounded diagnostic query.</summary>
    [Fact]
    public async Task StatisticsReadHonorsCancellation()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.GetRuntimeStatisticsAsync(cancelled.Token).AsTask());
    }
}
