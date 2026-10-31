using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks the dispatch contract using controlled state, without SQLite durability or external sends.</summary>
public sealed class ReservedDispatchContractTests
{
    private static async Task<(InMemorySessionStore Store, ExecutionReservation Reservation, SubmitRequest Submission)> ReserveAsync()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        SubmitRequest submission = new(session.SessionId, "  neue Eingabe\nα🙂\r\n", TimeSpan.FromMilliseconds(12345), "ordinal-workspace", true);
        await store.CommitAcceptanceAsync(submission, 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        return (store, reservation, submission);
    }

    private static ProviderReply Success(ProviderBinding? binding) => new(
        RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "complete output", true, binding, null);

    /// <summary>Every dispatch input comes from the accepted submission and reserved session.</summary>
    [Fact]
    public async Task CapturesCompleteSubmissionAndConsistentIdentity()
    {
        var (store, reservation, submission) = await ReserveAsync();
        ReservedDispatch? snapshot = await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(reservation, snapshot.Reservation);
        Assert.Equal(submission, snapshot.Submission);
        Assert.Equal(submission.Input, snapshot.Submission.Input);
        Assert.Equal(submission.Timeout, snapshot.Submission.Timeout);
        Assert.Equal(reservation.SessionId, snapshot.Session.SessionId);
        Assert.Equal(snapshot.Session.SessionId, snapshot.Submission.SessionId);
        Assert.Equal(1, snapshot.Sequence);
        Assert.Equal(0, snapshot.BindingRevision);
        Assert.Equal(snapshot.Session.BindingRevision, snapshot.BindingRevision);
        Assert.NotNull(snapshot.Session.Binding);
        Assert.Equal("native-session", snapshot.Session.Binding.SessionReference);
        Assert.Equal("ordinal-workspace", snapshot.Reservation.WorkspaceKey);
    }

    /// <summary>Later host settings and confirmed binding changes do not rewrite a reserved call.</summary>
    [Fact]
    public async Task FreezesSessionSettingsBindingAndRevision()
    {
        Dictionary<string, string> settings = new() { ["setting"] = "original" };
        InMemorySessionStore store = new();
        SessionSnapshot original = SchedulingData.Session();
        SessionSnapshot session = original with
        {
            Context = new(original.SessionId, "original-model", ExecutionKind.AgentTask,
                new(Path.GetTempPath(), "original-workspace"), settings),
        };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(session.SessionId, "new input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        settings["setting"] = "changed";
        await store.CommitSessionBindingAsync(session.SessionId, session.Binding! with { ContinuationState = "later-head" }, CancellationToken.None);

        ReservedDispatch? frozen = await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None);

        Assert.NotNull(frozen);
        Assert.Equal(session, frozen.Session);
        Assert.Equal("original", frozen.Session.Context.Settings["setting"]);
        Assert.Equal("original-model", frozen.Session.Context.ModelId);
        Assert.Equal("original-workspace", frozen.Session.Context.ExecutionContext.WorkspaceKey);
        Assert.Equal(0, frozen.BindingRevision);
        Assert.Equal(1, store.FindSession(session.SessionId)?.BindingRevision);
        Assert.Null(frozen.Session.Binding?.ContinuationState);
    }

    /// <summary>Read is repeatable and unknown IDs cannot grant permission.</summary>
    [Fact]
    public async Task ReadsWithoutChangingStateAndHonorsCancellation()
    {
        var (store, reservation, _) = await ReserveAsync();
        ReservedDispatch? first = await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None);
        Assert.NotNull(first);
        Assert.Equal(first, await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None));
        Assert.Null(await ((ISessionQueryStore)store).GetReservedDispatchAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Single(store.Reservations);
        Assert.Empty(store.DispatchState.History);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, cancellation.Token).AsTask());
    }

    /// <summary>Cancelled or locally ended work cannot be projected as a new send.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuppressesCancelledOrNonRunningRequests(bool cancelled)
    {
        var (store, reservation, _) = await ReserveAsync();
        Assert.NotNull(await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None));
        if (cancelled)
        {
            store.DispatchState.RequestCancellation(reservation.RequestId);
        }
        else
        {
            await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        }

        Assert.Null(await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None));
        Assert.Single(store.Reservations);
    }

    /// <summary>Unimplemented completion evidence cannot silently release the controlled reservation.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.Unknown)]
    [InlineData(ExecutionCertainty.InProgress)]
    public async Task RefusesUnmodelledCompletionWithoutRelease(ExecutionCertainty certainty)
    {
        var (store, reservation, _) = await ReserveAsync();
        ProviderReply reply = Success(null) with { Certainty = certainty };

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None).AsTask());

        Assert.Equal(reservation, store.Reservations[reservation.DispatchId]);
        Assert.Empty(store.DispatchState.History);
        Assert.Null((await ((ISessionQueryStore)store).GetRequestAsync(reservation.RequestId, CancellationToken.None))?.Result);
    }

    /// <summary>A stale revision changes no result, history, binding or reservation.</summary>
    [Fact]
    public async Task RejectsCompletionRevisionConflictWithoutPartialCommit()
    {
        var (store, reservation, _) = await ReserveAsync();
        ProviderBinding newer = store.FindSession(reservation.SessionId)!.Binding! with { ContinuationState = "newer-head" };
        await store.CommitSessionBindingAsync(reservation.SessionId, newer, CancellationToken.None);
        ISessionQueryStore queries = store;
        RequestSnapshot? before = await queries.GetRequestAsync(reservation.RequestId, CancellationToken.None);
        SessionSnapshot? sessionBefore = store.FindSession(reservation.SessionId);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            store.CommitCompletionAsync(reservation.DispatchId, Success(newer with { ContinuationState = "stale-result" }), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
        Assert.Equal(before, await queries.GetRequestAsync(reservation.RequestId, CancellationToken.None));
        Assert.Equal(sessionBefore, store.FindSession(reservation.SessionId));
        Assert.Empty(store.DispatchState.History);
        Assert.Equal(reservation, store.Reservations[reservation.DispatchId]);
    }

    /// <summary>Confirmed completion publishes result/history/binding once, advances revision and revokes permission.</summary>
    [Fact]
    public async Task CommitsCompletionOnceAndCapturesNextRevision()
    {
        var (store, reservation, submission) = await ReserveAsync();
        ProviderBinding binding = store.FindSession(reservation.SessionId)!.Binding! with { ContinuationState = "confirmed-head" };
        ProviderReply reply = Success(binding);
        await store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None);

        Assert.Null(await ((ISessionQueryStore)store).GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None));
        Assert.Empty(store.Reservations);
        RequestSnapshot? result = await ((ISessionQueryStore)store).GetRequestAsync(reservation.RequestId, CancellationToken.None);
        Assert.Equal(reply, result?.Result);
        Assert.Equal(RequestState.Succeeded, result?.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, result?.Certainty);
        HistoryEntry history = Assert.Single(store.DispatchState.History);
        Assert.NotEqual(Guid.Empty, history.HistoryId);
        Assert.Equal(new HistoryEntry(reservation.SessionId, reservation.RequestId, 1, submission.Input, reply)
        { HistoryOrdinal = 1, HistoryId = history.HistoryId }, history);
        Assert.Equal(binding, store.FindSession(reservation.SessionId)?.Binding);
        Assert.Equal(1, store.FindSession(reservation.SessionId)?.BindingRevision);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
        Assert.Single(store.DispatchState.History);

        await store.CommitAcceptanceAsync(submission with { Input = "only the next message" }, 100, CancellationToken.None);
        ExecutionReservation? next = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(next);
        ReservedDispatch? snapshot = await ((ISessionQueryStore)store).GetReservedDispatchAsync(next.DispatchId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Sequence);
        Assert.Equal(1, snapshot.BindingRevision);
        Assert.Equal(binding, snapshot.Session.Binding);
        Assert.Equal("only the next message", snapshot.Submission.Input);
    }
}
