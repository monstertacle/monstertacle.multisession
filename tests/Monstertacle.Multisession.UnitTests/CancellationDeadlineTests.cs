using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks that durable cancellation reaches active execution and cannot erase unknown work.</summary>
public sealed class CancellationDeadlineTests
{
    /// <summary>Intent cancels the adapter token; token acknowledgement alone does not prove external termination.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservesDurableRunningIntent(bool cooperative)
    {
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native", null),
            Sending = async (call, binding, observe, token) =>
            {
                observed = token;
                return cooperative ? await finish.Task.WaitAsync(token) : await finish.Task;
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Task<bool> dispatch = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await new RequestAdmission(scenario.Store, scenario.Options).CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => dispatch.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.Cancelled, failure.Failure.Code);
            Assert.True(observed.IsCancellationRequested);
            RequestSnapshot request = (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            Assert.Equal(RequestState.Cancelled, request.State);
            Assert.Equal(ExecutionCertainty.Unknown, request.Certainty);
            Assert.NotNull(request.CancellationRequestedUtc);
            Assert.Single(scenario.Store.Reservations);
        }
        finally { finish.TrySetResult(DispatchScenario.Success(scenario.Session.Binding!)); }
    }

    /// <summary>An intent committed between reservation and snapshot read prevents send and can safely end NotStarted.</summary>
    [Fact]
    public async Task CancelsBeforeSnapshotWithoutSending()
    {
        InMemorySessionStore? store = null;
        store = new()
        {
            DispatchState = new()
            {
                Reading = async token =>
                {
                    Guid id = Assert.Single(store!.Reservations).Value.RequestId;
                    await ((ISessionStore)store).RequestCancellationAsync(id, token);
                },
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        RequestSnapshot request = (await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Cancelled, request.State);
        Assert.Equal(ExecutionCertainty.NotStarted, request.Certainty);
        Assert.Empty(store.Reservations);
    }
}
