using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks bounded external waits and conservative handling of late adapter results.</summary>
public sealed class DispatchDeadlineTests
{
    /// <summary>A caller cancellation or execution deadline cannot free unconfirmed work, even if the adapter ignores its token.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BoundsExecutionAndIgnoresLateSuccess(bool timeout, bool cooperative)
    {
        using DispatchClock clock = new();
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        ControlledProvider provider = new()
        {
            Sending = async (call, binding, observe, token) =>
            {
                observedToken = token;
                try { return cooperative ? await finish.Task.WaitAsync(token) : await finish.Task; }
                finally { ended.TrySetResult(); }
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, clock: clock);
        RequestReceipt receipt = await scenario.SubmitAsync(writeKey: "workspace");
        using CancellationTokenSource caller = new();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(caller.Token).AsTask();
        Assert.Same(provider.Started.Task, await Task.WhenAny(provider.Started.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
        if (timeout)
        {
            Assert.Equal(TimeSpan.FromSeconds(10), clock.DueTime);
            clock.Fire();
        }
        else
        {
            caller.Cancel();
        }

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(timeout ? ErrorCode.TimedOut : ErrorCode.Cancelled, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.True(observedToken.IsCancellationRequested);
        Assert.Single(scenario.Store.Reservations);
        finish.TrySetResult(DispatchScenario.Success(scenario.Session.Binding!));
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        RequestSnapshot? request = await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Equal(timeout ? RequestState.TimedOut : RequestState.Cancelled, request?.State);
        Assert.Equal(ExecutionCertainty.Unknown, request?.Certainty);
        Assert.Empty(scenario.Store.DispatchState.History);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
    }

    /// <summary>Known final evidence can still commit when caller cancellation arrives during the store transaction.</summary>
    [Fact]
    public async Task PreservesKnownFinalResultDespiteCallerCancellationDuringCommit()
    {
        using CancellationTokenSource caller = new();
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Committing = token => { caller.Cancel(); token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; } },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(caller.Token));
        Assert.Equal(RequestState.Succeeded, (await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
        Assert.Empty(store.Reservations);
    }

    /// <summary>Cancellation before reservation does not start provider work or consume a slot.</summary>
    [Fact]
    public async Task PreCancelledCallerCreatesNoReservation()
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        await scenario.SubmitAsync();
        using CancellationTokenSource caller = new();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scenario.Dispatcher.TryDispatchAsync(caller.Token).AsTask());
        Assert.Empty(scenario.Store.Reservations);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
    }
}
