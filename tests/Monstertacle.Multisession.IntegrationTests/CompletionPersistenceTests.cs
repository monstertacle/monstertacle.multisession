using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks result/history/binding/release as one durable transaction.</summary>
public sealed class CompletionPersistenceTests
{
    /// <summary>Confirmed completion publishes all state and the next FIFO input sees the new head.</summary>
    [Fact]
    public async Task CommitsKnownResultHistoryBindingAndReleaseTogether()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        RequestSnapshot request = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Succeeded, request.State);
        Assert.Equal("complete output", request.Result?.Output);
        Assert.NotNull(request.EndedUtc);
        Assert.Equal(TimeSpan.Zero, request.EndedUtc.Value.Offset);
        SessionSnapshot session = (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))!;
        Assert.Equal("new-head", session.Binding?.ContinuationState);
        Assert.Equal(fixture.Session.BindingRevision + 1, session.BindingRevision);
        Assert.Equal(request.EndedUtc, session.UpdatedUtc);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        HistoryEntry history = Assert.Single((await store.ReadHistoryAsync(session.SessionId, 100, null, CancellationToken.None)).Items);
        Assert.Equal(fixture.Receipt.RequestId, history.RequestId);
        Assert.Equal("  original input\n🙂\r\n", history.Input);
        Assert.Equal("complete output", history.Result.Output);
        Assert.Equal(1, history.HistoryOrdinal);
        Assert.NotEqual(Guid.Empty, history.HistoryId);
        Assert.Null(await store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None));
        RequestReceipt next = await store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "next input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        ExecutionReservation reservation = (await store.TryReserveHeadAsync(4, CancellationToken.None))!;
        ReservedDispatch dispatch = (await store.GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None))!;
        Assert.Equal(next.RequestId, reservation.RequestId);
        Assert.Equal("new-head", dispatch.Session.Binding?.ContinuationState);
    }

    /// <summary>Inclusive output bytes are enforced at the authoritative store boundary as well.</summary>
    [Theory]
    [InlineData("🙂", false)]
    [InlineData("🙂a", true)]
    public async Task EnforcesOutputLimitWithoutTruncatedSuccess(string output, bool excessive)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync(4);
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(output), CancellationToken.None);
        ProviderReply result = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!.Result!;
        Assert.Equal(excessive ? RequestState.Failed : RequestState.Succeeded, result.Outcome);
        Assert.Equal(excessive ? string.Empty : output, result.Output);
        Assert.Equal(!excessive, result.OutputComplete);
        Assert.Equal(excessive ? ErrorCode.OutputLimitExceeded : null, result.Error?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, result.Certainty);
    }

    /// <summary>Concurrent duplicate completion has exactly one result/history/release winner.</summary>
    [Fact]
    public async Task RejectsDuplicateCompletionWithoutAppendingHistory()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        Task<bool>[] attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None); return true; }
            catch (SessionOperationException failure) { Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code); return false; }
        }).ToArray();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(value => value));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Single((await fixture.Host.Store.ReadHistoryAsync(fixture.Session.SessionId, 100, null, CancellationToken.None)).Items);
    }

    /// <summary>A changed confirmed revision cannot be overwritten by an old dispatch result.</summary>
    [Fact]
    public async Task RejectsStaleBindingRevisionWithoutPartialChanges()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitSessionBindingAsync(fixture.Session.SessionId, fixture.Session.Binding! with { ContinuationState = "concurrent-head" }, CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        Assert.Equal("concurrent-head", (await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.Binding?.ContinuationState);
    }

    /// <summary>A real error after result/history writes cannot publish any part of completion or free the workspace.</summary>
    [Fact]
    public async Task RollsBackFailedCompletionCommitWithoutReleasingReservation()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "CREATE TRIGGER FailCompletion AFTER UPDATE OF BindingRevision ON Sessions BEGIN SELECT RAISE(FAIL,'private-completion-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private-completion-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        RequestSnapshot request = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Running, request.State);
        Assert.Null(request.Result);
        Assert.Equal(fixture.Session.Binding, (await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.Binding);
    }

    /// <summary>Later provider errors append their own entry and never rewrite a prior successful observation.</summary>
    [Fact]
    public async Task PreservesExistingHistoryWhenProviderContextFails()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        string original = (string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History WHERE HistoryOrdinal=1")!;
        await store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(fixture.Session.SessionId, "later", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        ExecutionReservation next = (await store.TryReserveHeadAsync(4, CancellationToken.None))!;
        await store.CommitCompletionAsync(next.DispatchId, new(RequestState.Failed, ExecutionCertainty.NotStarted, string.Empty, false, null, new(ErrorCode.ProviderContextLimit)), CancellationToken.None);
        Assert.Equal(original, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History WHERE HistoryOrdinal=1"));
        Page<HistoryEntry> page = await store.ReadHistoryAsync(fixture.Session.SessionId, 1, null, CancellationToken.None);
        Assert.Equal(RequestState.Succeeded, Assert.Single(page.Items).Result.Outcome);
        Assert.NotNull(page.NextCursor);
        Page<HistoryEntry> second = await store.ReadHistoryAsync(fixture.Session.SessionId, 1, page.NextCursor, CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderContextLimit, Assert.Single(second.Items).Result.Error?.Code);
        Assert.Equal(2, second.Items[0].HistoryOrdinal);
        Assert.Null(second.NextCursor);
    }
}
