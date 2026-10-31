using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Tests fresh dispatch, frozen input and confirmed completion with a controlled store.</summary>
public sealed class RequestDispatcherTests
{
    /// <summary>Running and reservation precede Send; completion remains the only release point.</summary>
    [Fact]
    public async Task SendsFrozenInputAfterReservationAndCommitsBeforeReturning()
    {
        ControlledProvider provider = new() { OpeningBinding = new("controlled", 1, "native-session", null) };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync(writeKey: "workspace");
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        Task first = await Task.WhenAny(provider.Started.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(provider.Started.Task, first);
        ProviderCall call = await provider.Started.Task;
        Assert.False(pending.IsCompleted);
        ExecutionReservation reservation = Assert.Single(scenario.Store.Reservations).Value;
        Assert.Equal(reservation.DispatchId, call.DispatchId);
        Assert.Equal(receipt.RequestId, call.RequestId);
        Assert.Equal(scenario.Session.Context, call.Session);
        Assert.Equal("  only new input\nα🙂\r\n", call.Input);
        Assert.Equal(TimeSpan.FromSeconds(10), call.Timeout);
        Assert.Equal(scenario.Options.MaxOutputBytes, call.MaxOutputBytes);
        Assert.Equal(RequestState.Running, (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
        ProviderReply reply = DispatchScenario.Success(scenario.Session.Binding! with { SessionReference = "native-session", ContinuationState = "head-1" });
        provider.Completion.SetResult(reply);

        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(scenario.Store.Reservations);
        Assert.Equal(reply, (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))?.Result);
        Assert.Equal(reply.Binding, scenario.Store.FindSession(scenario.Session.SessionId)?.Binding);
        Assert.Single(scenario.Store.DispatchState.History);
    }

    /// <summary>A second call contains only new input and the last committed continuation.</summary>
    [Fact]
    public async Task ContinuesBindingWithoutReplayingInput()
    {
        ConcurrentQueue<(ProviderCall Call, ProviderBinding Binding)> calls = new();
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "stable-native", "initial"),
            Sending = (call, binding, observe, token) =>
            {
                calls.Enqueue((call, binding));
                return ValueTask.FromResult(DispatchScenario.Success(binding with { ContinuationState = call.Input }));
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync("first");
        await scenario.SubmitAsync("second");
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));

        var observed = calls.ToArray();
        Assert.Equal(2, observed.Length);
        Assert.Equal("first", observed[0].Call.Input);
        Assert.Equal("initial", observed[0].Binding.ContinuationState);
        Assert.Equal("second", observed[1].Call.Input);
        Assert.Equal("first", observed[1].Binding.ContinuationState);
        Assert.Equal("stable-native", observed[1].Binding.SessionReference);
        Assert.NotEqual(observed[0].Call.DispatchId, observed[1].Call.DispatchId);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(2, scenario.Store.FindSession(scenario.Session.SessionId)?.BindingRevision);
    }

    /// <summary>No eligible head produces no send or history.</summary>
    [Fact]
    public async Task EmptyQueueDoesNotStartProvider()
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        Assert.Empty(scenario.Store.DispatchState.History);
    }

    /// <summary>A cancellation recorded between reservation and read revokes send permission.</summary>
    [Fact]
    public async Task RevokedProjectionDoesNotSend()
    {
        InMemorySessionStore? store = null;
        store = new()
        {
            DispatchState = new()
            {
                Reading = token =>
                {
                    store!.DispatchState.RequestCancellation(Assert.Single(store.Reservations).Value.RequestId);
                    return ValueTask.CompletedTask;
                },
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        await scenario.SubmitAsync();
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        Assert.Single(store.Reservations);
    }

    /// <summary>Query failures are visible and cannot be confused with an empty queue.</summary>
    [Fact]
    public async Task ReportsSnapshotStoreFailureWithoutSending()
    {
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Reading = token => throw new IOException("private store details") },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        Assert.Single(store.Reservations);
    }

    /// <summary>A lost completion acknowledgement cannot trigger retry or erase a possibly committed success.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitsForCompletionAndNeverRetriesAfterCommitFailure(bool fail)
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            DispatchState = new()
            {
                Committing = async token =>
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(token);
                    if (fail) { throw new IOException("private store details"); }
                },
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        await scenario.SubmitAsync();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        Assert.Same(entered.Task, await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(pending.IsCompleted);
        Assert.Single(store.Reservations);
        Assert.Empty(store.DispatchState.History);
        release.TrySetResult();
        if (fail)
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => pending);
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Single(store.Reservations);
            Assert.Empty(store.DispatchState.History);
        }
        else
        {
            Assert.True(await pending);
            Assert.Empty(store.Reservations);
        }
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
    }
}
