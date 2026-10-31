using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks durable cancellation through the public facade and its atomic store boundary.</summary>
public sealed class RequestCancellationTests
{
    /// <summary>Queued cancellation never dispatches, is idempotent and survives reopening with original input.</summary>
    [Fact]
    public async Task CancelsQueuedWithoutSendAndPersistsOnce()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAdmission requests = new(fixture.Store, fixture.Options);
        RequestReceipt receipt = await requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "  original\n🙂", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        await requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
        RequestSnapshot cancelled = (await requests.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Cancelled, cancelled.State);
        Assert.Equal(ExecutionCertainty.NotStarted, cancelled.Certainty);
        Assert.NotNull(cancelled.CancellationRequestedUtc);
        Assert.Equal(cancelled.EndedUtc, cancelled.CancellationRequestedUtc);
        Assert.Null(cancelled.StartedUtc);
        await requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Null(await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None));
        await fixture.ReopenAsync();
        Assert.Equal(cancelled, await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None));
        HistoryEntry history = Assert.Single((await fixture.Store.ReadHistoryAsync(session.SessionId, 100, null, CancellationToken.None)).Items);
        Assert.Equal("  original\n🙂", history.Input);
        Assert.Equal(RequestState.Cancelled, history.Result.Outcome);
    }

    /// <summary>Running intent cannot claim end or prevent a later known completion from winning.</summary>
    [Fact]
    public async Task RecordsRunningIntentWithoutReleasingAndAllowsKnownCompletion()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        RequestSnapshot before = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        RequestSnapshot intent = await fixture.Host.Store.RequestCancellationAsync(before.RequestId, CancellationToken.None);
        Assert.NotNull(intent.CancellationRequestedUtc);
        Assert.Equal(before with { CancellationRequestedUtc = intent.CancellationRequestedUtc }, intent);
        Assert.Equal(intent, await fixture.Host.Store.RequestCancellationAsync(before.RequestId, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        Assert.Null(await fixture.Host.Store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None));
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        RequestSnapshot completed = (await fixture.Host.Store.GetRequestAsync(before.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Succeeded, completed.State);
        Assert.Equal(intent.CancellationRequestedUtc, completed.CancellationRequestedUtc);
        Assert.Equal(completed, await fixture.Host.Store.RequestCancellationAsync(before.RequestId, CancellationToken.None));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Missing IDs and cancellation before the write do not invent accepted or terminal state.</summary>
    [Fact]
    public async Task RejectsMissingAndPreCancelledWithoutMutation()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        SessionOperationException missing = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Store.RequestCancellationAsync(Guid.NewGuid(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.NotFound, missing.Failure.Code);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Host.Store.RequestCancellationAsync(fixture.Receipt.RequestId, cancelled.Token).AsTask());
        Assert.Null((await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))?.CancellationRequestedUtc);
    }

    /// <summary>Cancellation intent cannot discard native evidence from an already running handoff.</summary>
    [Fact]
    public async Task PreservesLateNativeEvidenceWithoutClaimingTermination()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.RequestCancellationAsync(fixture.Receipt.RequestId, CancellationToken.None);
        await store.CommitObservationAsync(fixture.Reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "accepted-operation"), CancellationToken.None);
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RequestSnapshot unknown = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(unknown, await store.RequestCancellationAsync(unknown.RequestId, CancellationToken.None));
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, unknown.RequestId), CancellationToken.None))!;
        Assert.Equal("accepted-operation", item.OperationReference);
        Assert.Equal(ExecutionCertainty.Unknown, unknown.Certainty);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>A failed write after result/history insertion rolls back the entire queued cancellation.</summary>
    [Fact]
    public async Task RollsBackFailedIntentWithoutPublishingPartialResult()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestReceipt receipt = await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(Guid.NewGuid(), new(session.SessionId, "queued", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        StoreProbe.Execute(fixture.Directory.DatabasePath, "CREATE TRIGGER FailCancel AFTER UPDATE OF CancellationRequestedUtc ON Requests BEGIN SELECT RAISE(FAIL,'private-cancel-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.RequestCancellationAsync(receipt.RequestId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private-cancel-marker", failure.ToString(), StringComparison.Ordinal);
        RequestSnapshot request = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Queued, request.State);
        Assert.Null(request.CancellationRequestedUtc);
        Assert.Null(request.Result);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Results"));
    }

    /// <summary>Cancel and reserve share the same atomic boundary in either enqueue order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializesCancelAgainstReservation(bool cancelFirst)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestReceipt receipt = await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(Guid.NewGuid(), new(session.SessionId, "race", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        Task<RequestSnapshot> cancel;
        Task<ExecutionReservation?> reserve;
        if (cancelFirst)
        {
            cancel = fixture.Store.RequestCancellationAsync(receipt.RequestId, CancellationToken.None).AsTask();
            reserve = fixture.Store.TryReserveHeadAsync(4, CancellationToken.None).AsTask();
        }
        else
        {
            reserve = fixture.Store.TryReserveHeadAsync(4, CancellationToken.None).AsTask();
            cancel = fixture.Store.RequestCancellationAsync(receipt.RequestId, CancellationToken.None).AsTask();
        }
        RequestSnapshot request = await cancel;
        Assert.NotNull(request.CancellationRequestedUtc);
        Assert.Equal(cancelFirst ? RequestState.Cancelled : RequestState.Running, request.State);
        Assert.Equal(cancelFirst, await reserve is null);
        Assert.Equal(cancelFirst ? 1L : 0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(cancelFirst ? 0L : 1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
