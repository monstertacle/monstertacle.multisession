using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks durable cases and reserved uncertainty without claiming a real process crash.</summary>
public sealed class RecoveryPersistenceTests
{
    /// <summary>Unknown keeps reservation, confirmed head and original identity, but ends local running state.</summary>
    [Fact]
    public async Task PersistsUnknownAndKnownOperationWithoutReleasingSlot()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.CommitObservationAsync(fixture.Reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "known-operation"), CancellationToken.None);
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RequestSnapshot request = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Interrupted, request.State);
        Assert.Equal(ExecutionCertainty.Unknown, request.Certainty);
        Assert.NotNull(request.EndedUtc);
        Assert.Equal(fixture.Session.Binding, (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.Binding);
        Assert.Equal(SessionBlockReason.RecoveryRequired, (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.BlockReason);
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, request.RequestId), CancellationToken.None))!;
        Assert.Equal("known-operation", item.OperationReference);
        Assert.Equal(fixture.Reservation.DispatchId, item.DispatchId);
        Assert.Equal(fixture.Session.Binding, item.Binding);
        Assert.Equal(1, item.Revision);
        Assert.Equal(RecoveryCaseStatus.Open, item.Status);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        Assert.Null(await store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None));
        Assert.Single((await store.ListRecoveryCasesAsync(1, null, CancellationToken.None)).Items);
    }

    /// <summary>Opening uncertainty is discoverable without a made-up request, even after a binding commit.</summary>
    [Fact]
    public async Task PersistsOpeningCaseForAlreadyConfirmedLocalBinding()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        RecoveryCaseSnapshot item = (await fixture.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, session.SessionId), CancellationToken.None))!;
        Assert.NotNull(item);
        Assert.Equal(session.SessionId, item.SessionId);
        Assert.Null(item.RequestId);
        Assert.Null(item.DispatchId);
        Assert.Equal(session.Binding, item.Binding);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Reservations"));
        RecoveryCaseSnapshot again = (await fixture.Store.GetRecoveryCaseAsync(item.Target, CancellationToken.None))!;
        Assert.Equal(item, again);
    }

    /// <summary>A first native identity acknowledged before an uncertain reply is retained only as case evidence.</summary>
    [Fact]
    public async Task PreservesFirstObservedNativeIdentityForLazyRecovery()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        Guid id = Guid.NewGuid();
        ProviderSessionContext context = new(id, "model", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
        await fixture.Store.CreateSessionAsync(new(id, "connection-I", "profile", context, null) { SettingsRevision = "revision" }, CancellationToken.None);
        await fixture.Store.CommitSessionBindingAsync(id, new("fixture-adapter", 7, null, null), CancellationToken.None);
        RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(id, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        ExecutionReservation reservation = (await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None))!;
        await fixture.Store.CommitObservationAsync(reservation.DispatchId, new(ProviderObservationKind.SessionEstablished, "first-native"), CancellationToken.None);
        await fixture.Store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await fixture.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, receipt.RequestId), CancellationToken.None))!;
        Assert.Equal("first-native", item.Binding?.SessionReference);
        Assert.Null(item.Binding?.ContinuationState);
        Assert.Null((await fixture.Store.GetSessionAsync(id, CancellationToken.None))?.Binding?.SessionReference);
    }

    /// <summary>Recovery paging is scoped and preserves case creation/revision rather than leaking request contents.</summary>
    [Fact]
    public async Task PagesRecoveryCasesByStableFullKey()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        for (int index = 0; index < 3; index++)
        {
            SessionSnapshot session = await fixture.CreateSessionAsync();
            await fixture.Store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        }
        List<RecoveryCaseSnapshot> cases = [];
        string? cursor = null;
        do
        {
            Page<RecoveryCaseSnapshot> page = await fixture.Store.ListRecoveryCasesAsync(1, cursor, CancellationToken.None);
            cases.Add(Assert.Single(page.Items));
            cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(3, cases.Select(item => item.Target).Distinct().Count());
        Assert.All(cases, item => Assert.Equal(1, item.Revision));
        Assert.Null(await fixture.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, Guid.NewGuid()), CancellationToken.None));
    }
}
