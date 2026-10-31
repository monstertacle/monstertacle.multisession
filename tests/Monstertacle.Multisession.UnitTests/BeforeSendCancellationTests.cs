using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Cancellation after reservation remains a known local non-start, without a false storage fault.</summary>
public sealed class BeforeSendCancellationTests
{
    /// <summary>The pre-send cancellation read has the same cancellation contract as the frozen dispatch read.</summary>
    [Fact]
    public async Task CancellationDuringPreSendReadCommitsCancelledNotStarted()
    {
        using CancellationTokenSource stop = new();
        InMemorySessionStore store = new() { ReadingRequest = (_, _) => { stop.Cancel(); return ValueTask.CompletedTask; } };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(stop.Token).AsTask());
        Assert.Equal(ErrorCode.Cancelled, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Equal(receipt.RequestId, failure.Failure.RequestId);
        Assert.Equal(scenario.Session.SessionId, failure.Failure.SessionId);
        Assert.Equal(RequestState.Cancelled, store.Data.Requests[receipt.RequestId].State);
        Assert.Equal(ExecutionCertainty.NotStarted, store.Data.Requests[receipt.RequestId].Certainty);
        Assert.Empty(store.Reservations);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
    }
}
