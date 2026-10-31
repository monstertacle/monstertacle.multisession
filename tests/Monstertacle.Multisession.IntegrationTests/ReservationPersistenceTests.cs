using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks the durable attempt fence and unchanged reserved execution input.</summary>
public sealed class ReservationPersistenceTests
{
    /// <summary>The attempt is committed before it can be read as permission, and later binding changes cannot rewrite it.</summary>
    [Fact]
    public async Task ReservesRunningSnapshotBeforeExternalExecution()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        ReservedDispatch saved = (await store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None))!;
        Assert.NotNull(saved);
        Assert.Equal(fixture.Receipt.RequestId, saved.Reservation.RequestId);
        Assert.Equal(fixture.Receipt.Sequence, saved.Sequence);
        Assert.Equal("  original input\n🙂\r\n", saved.Submission.Input);
        Assert.Equal(fixture.Session.Binding, saved.Session.Binding);
        Assert.Equal(fixture.Session.BindingRevision, saved.BindingRevision);
        RequestSnapshot request = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Running, request.State);
        Assert.Equal(ExecutionCertainty.InProgress, request.Certainty);
        Assert.Equal(fixture.Reservation.ReservedUtc, request.StartedUtc);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        await store.CommitSessionBindingAsync(fixture.Session.SessionId, fixture.Session.Binding! with { ContinuationState = "later-binding" }, CancellationToken.None);
        ReservedDispatch frozen = (await store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None))!;
        Assert.Equal(saved.Session.Binding, frozen.Session.Binding);
        Assert.Equal(saved.BindingRevision, frozen.BindingRevision);
        Assert.Null(await store.GetReservedDispatchAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>Slot, session FIFO and shared write workspace are one atomic eligibility decision.</summary>
    [Fact]
    public async Task SkipsBlockedWorkspaceHeadWithoutBypassingSessionFifo()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot first = await fixture.Catalog.CreateSessionAsync(Guid.NewGuid(), "profile-İ", new(null, "shared"), CancellationToken.None);
        SessionSnapshot second = await fixture.Catalog.CreateSessionAsync(Guid.NewGuid(), "profile-İ", new(null, "shared"), CancellationToken.None);
        SessionSnapshot third = await fixture.Catalog.CreateSessionAsync(Guid.NewGuid(), "profile-İ", new(null, "independent"), CancellationToken.None);
        List<RequestReceipt> accepted = [];
        foreach (SessionSnapshot session in new[] { first, first, second, third })
        {
            accepted.Add(await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), session.Context.ExecutionContext.WorkspaceKey, true)), 100, CancellationToken.None));
        }
        ExecutionReservation one = (await fixture.Store.TryReserveHeadAsync(2, CancellationToken.None))!;
        ExecutionReservation two = (await fixture.Store.TryReserveHeadAsync(2, CancellationToken.None))!;
        Assert.Equal(accepted[0].RequestId, one.RequestId);
        Assert.Equal(accepted[3].RequestId, two.RequestId);
        Assert.Null(await fixture.Store.TryReserveHeadAsync(2, CancellationToken.None));
        Assert.Equal(RequestState.Queued, (await fixture.Store.GetRequestAsync(accepted[1].RequestId, CancellationToken.None))?.State);
        Assert.Equal(RequestState.Queued, (await fixture.Store.GetRequestAsync(accepted[2].RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Concurrent workers cannot overbook a global slot or reserve the same session twice.</summary>
    [Fact]
    public async Task AppliesReservationConstraintsUnderConcurrentWorkers()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        for (int index = 0; index < 3; index++)
        {
            SessionSnapshot session = await fixture.CreateSessionAsync();
            await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        }
        ExecutionReservation?[] results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => fixture.Store.TryReserveHeadAsync(2, CancellationToken.None).AsTask()));
        Assert.Equal(2, results.Count(item => item is not null));
        Assert.Equal(2, results.Where(item => item is not null).Select(item => item!.SessionId).Distinct().Count());
        Assert.Equal(2L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Fresh-process state does not grant send permission merely because the old snapshot remains durable.</summary>
    [Fact]
    public async Task ReopenRetainsMarkerWithoutGrantingOldDispatchPermission()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        string before = (string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests")!;
        using JsonDocument snapshot = JsonDocument.Parse(before);
        Assert.Equal(fixture.Reservation.DispatchId, snapshot.RootElement.GetProperty("Snapshot").GetProperty("Reservation").GetProperty("DispatchId").GetGuid());
        await fixture.Host.Store.DisposeAsync();
        await using ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(fixture.Host.Directory, fixture.Host.Directory.TrustScopeId, fixture.Host.Options, CancellationToken.None);
        Assert.Null(await reopened.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None));
        Assert.Equal(before, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests"));
    }
}
