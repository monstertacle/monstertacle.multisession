using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks awaitable observation acknowledgement independently from terminal success.</summary>
public sealed class DispatchObservationTests
{
    /// <summary>A provider may continue only after its dispatch-bound observation commit.</summary>
    [Fact]
    public async Task WaitsForObservationCommitBeforeExternalContinuation()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource continued = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            DispatchState = new()
            {
                Observing = async (observation, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); },
            },
        };
        ControlledProvider provider = new()
        {
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.SessionEstablished, "opaque-session"), token);
                await observe(new(ProviderObservationKind.OperationAccepted, "opaque-operation"), token);
                continued.TrySetResult();
                return DispatchScenario.Success(binding with { SessionReference = "opaque-session", ContinuationState = "confirmed-head" });
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, store);
        await scenario.SubmitAsync();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        Assert.Same(entered.Task, await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(continued.Task.IsCompleted);
        Assert.Null(store.FindSession(scenario.Session.SessionId)?.Binding?.SessionReference);
        Assert.Empty(store.DispatchState.Observations);
        Assert.Single(store.Reservations);
        release.TrySetResult();
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(continued.Task.IsCompleted);
        Assert.Equal(2, store.DispatchState.Observations.Count);
        Assert.All(store.DispatchState.Observations, item => Assert.Equal(Assert.Single(store.DispatchState.Snapshots).Key, item.DispatchId));
        Assert.Equal("confirmed-head", store.FindSession(scenario.Session.SessionId)?.Binding?.ContinuationState);
    }

    /// <summary>A provider catching a persistence failure cannot convert it into a trusted final result.</summary>
    [Fact]
    public async Task RejectsSuccessAfterSwallowedCallbackFailure()
    {
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Observing = (observation, token) => throw new IOException("private persistence failure") },
        };
        ControlledProvider provider = new()
        {
            Sending = async (call, binding, observe, token) =>
            {
                try { await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token); }
                catch (Exception) { /* Simulate a broken external adapter. */ }
                return DispatchScenario.Success(binding);
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, store);
        await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
        Assert.Single(store.Reservations);
        Assert.Empty(store.DispatchState.History);
    }

    /// <summary>An observed stable identity cannot replace an established native session.</summary>
    [Fact]
    public async Task RejectsObservedIdentityReplacement()
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "original-session", null),
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.SessionEstablished, "replacement-session"), token);
                return DispatchScenario.Success(binding);
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.Empty(scenario.Store.DispatchState.Observations);
        Assert.Single(scenario.Store.Reservations);
    }
    /// <summary>An unawaited callback cannot race the final response into a successful commit.</summary>
    [Fact]
    public async Task RejectsReplyWhileObservationIsStillPending()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? callback = null;
        InMemorySessionStore store = new()
        {
            DispatchState = new() { Observing = async (observation, token) => await release.Task.WaitAsync(token) },
        };
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) =>
            {
                callback = observe(new(ProviderObservationKind.OperationAccepted, "operation"), token).AsTask();
                return ValueTask.FromResult(DispatchScenario.Success(binding));
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, store);
        await scenario.SubmitAsync();
        await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Single(store.Reservations);
        Assert.Empty(store.DispatchState.History);
        release.TrySetResult();
        Assert.NotNull(callback);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => callback);
    }

    /// <summary>A callback invoked only after final completion cannot mutate the store.</summary>
    [Fact]
    public async Task RejectsObservationAfterDispatchHasClosed()
    {
        Func<ProviderObservation, CancellationToken, ValueTask>? callback = null;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "stable", null),
            Sending = (call, binding, observe, token) =>
            {
                callback = observe;
                return ValueTask.FromResult(DispatchScenario.Success(binding));
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync();
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.NotNull(callback);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => callback(new(ProviderObservationKind.OperationAccepted, "late"), CancellationToken.None).AsTask());
        Assert.Empty(scenario.Store.DispatchState.Observations);
        Assert.Single(scenario.Store.DispatchState.History);
        Assert.Empty(scenario.Store.Reservations);
    }

    /// <summary>A lazy native identity needs an acknowledged establishment before it can be confirmed by a reply.</summary>
    [Fact]
    public async Task RefusesLazyIdentityWithoutEstablishmentObservation()
    {
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) => ValueTask.FromResult(DispatchScenario.Success(binding with { SessionReference = "unobserved" })),
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync();
        await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Null(scenario.Store.FindSession(scenario.Session.SessionId)?.Binding?.SessionReference);
        Assert.Single(scenario.Store.Reservations);
        Assert.Empty(scenario.Store.DispatchState.History);
    }
}
