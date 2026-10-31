using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Exercises lost acknowledgements and failure boundaries without claiming SQLite persistence.</summary>
public sealed class DispatchStoreFailureTests
{
    /// <summary>A reservation failure is visible without exposing raw store diagnostics.</summary>
    [Fact]
    public async Task SanitizesReservationFailure()
    {
        InMemorySessionStore store = new() { Reserving = token => throw new IOException("private store detail") };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
    }

    /// <summary>A persisted success remains successful if only its acknowledgement is lost.</summary>
    [Fact]
    public async Task DoesNotOverwriteCommittedResultAfterLostAcknowledgement()
    {
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Completed = () => throw new IOException("private acknowledgement failure") },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        RequestSnapshot? request = await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Succeeded, request?.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request?.Certainty);
        Assert.Single(store.DispatchState.History);
        Assert.Empty(store.Reservations);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
    }

    /// <summary>A concurrent confirmed binding publication prevents all completion effects.</summary>
    [Fact]
    public async Task ReportsRevisionConflictWithoutRetryOrPartialCompletion()
    {
        InMemorySessionStore? store = null;
        store = new()
        {
            DispatchState = new()
            {
                Committing = async token =>
                {
                    ExecutionReservation reservation = Assert.Single(store!.Reservations).Value;
                    ProviderBinding current = store.FindSession(reservation.SessionId)!.Binding!;
                    await store.CommitSessionBindingAsync(reservation.SessionId, current with { ContinuationState = "concurrent-head" }, token);
                },
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
        RequestSnapshot? request = await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Running, request?.State);
        Assert.Null(request?.Result);
        Assert.Equal("concurrent-head", store.FindSession(scenario.Session.SessionId)?.Binding?.ContinuationState);
        Assert.Empty(store.DispatchState.History);
        Assert.Single(store.Reservations);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
    }

    /// <summary>Failure to save Unknown is reported as a storage failure and never as a successfully secured block.</summary>
    [Fact]
    public async Task ReportsFailureToPersistUncertainty()
    {
        InMemorySessionStore store = new()
        {
            DispatchState = new() { MarkingUncertain = token => throw new IOException("private store failure") },
        };
        ControlledProvider provider = new() { Sending = (call, binding, observe, token) => throw new IOException("private provider failure") };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(RequestState.Running, (await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
        Assert.Single(store.Reservations);
        Assert.Empty(store.DispatchState.History);
    }

    /// <summary>Cancellation while reading the snapshot still proves no external send occurred.</summary>
    [Fact]
    public async Task CommitsNotStartedCancellationBeforeSend()
    {
        using CancellationTokenSource caller = new();
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Reading = token => { caller.Cancel(); token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; } },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(caller.Token).AsTask());
        Assert.Equal(ErrorCode.Cancelled, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        RequestSnapshot? request = await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Cancelled, request?.State);
        Assert.Equal(ExecutionCertainty.NotStarted, request?.Certainty);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        Assert.Empty(store.Reservations);
    }
}
