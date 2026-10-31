using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Cancellation acknowledgement and confirmed external end have different capacity consequences.</summary>
public sealed class NativeCancellationTests
{
    /// <summary>Explicit end evidence from the original send or native cancel preserves the local cause and releases capacity.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UsesConfirmedEndWithoutReportingSuccess(bool native, bool timeout)
    {
        using DispatchClock clock = new();
        TaskCompletionSource accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int cancellations = 0;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native", "old-head"),
            Capabilities = new(true, true, true, native, false),
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                accepted.TrySetResult();
                try { return native ? await finish.Task : await finish.Task.WaitAsync(token); }
                catch (OperationCanceledException) { return new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, new(ErrorCode.Cancelled)); }
            },
            Cancelling = (context, operation, token) =>
            {
                Assert.Equal("operation", operation);
                Assert.False(token.IsCancellationRequested);
                Interlocked.Increment(ref cancellations);
                return ValueTask.FromResult(new ProviderReply(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false,
                    new("controlled", 1, "native", "verified-head"), new(ErrorCode.Cancelled)));
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, clock: clock);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (timeout) { clock.Fire(); }
            else { await new RequestAdmission(scenario.Store, scenario.Options).CancelRequestAsync(receipt.RequestId, CancellationToken.None); }
            Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
            RequestSnapshot result = (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            Assert.Equal(timeout ? RequestState.TimedOut : RequestState.Cancelled, result.State);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, result.Certainty);
            Assert.Equal(timeout ? ErrorCode.TimedOut : ErrorCode.Cancelled, result.Result?.Error?.Code);
            Assert.Empty(scenario.Store.Reservations);
            Assert.Single(scenario.Store.DispatchState.History);
            Assert.Equal(native ? 1 : 0, cancellations);
            Assert.Equal(native ? "verified-head" : "old-head", scenario.Store.FindSession(scenario.Session.SessionId)?.Binding?.ContinuationState);
        }
        finally { finish.TrySetResult(DispatchScenario.Success(scenario.Session.Binding!)); }
    }

    /// <summary>Native no-start or unknown evidence cannot release the original execution.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.NotStarted, false, false)]
    [InlineData(ExecutionCertainty.Unknown, false, false)]
    [InlineData(ExecutionCertainty.Unknown, true, false)]
    [InlineData(ExecutionCertainty.ConfirmedEnded, false, true)]
    public async Task DoesNotMistakeCancellationAcknowledgementForEnd(ExecutionCertainty certainty, bool ignoresToken, bool foreign)
    {
        TaskCompletionSource accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native", null),
            Capabilities = new(true, true, true, true, false),
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                accepted.TrySetResult();
                return await finish.Task;
            },
            Cancelling = async (context, operation, token) =>
            {
                Interlocked.Increment(ref calls);
                if (ignoresToken) { return await finish.Task; }
                return new(RequestState.Cancelled, certainty, string.Empty, false,
                    foreign ? new("controlled", 1, "foreign-native", null) : null, new(ErrorCode.Cancelled));
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await new RequestAdmission(scenario.Store, scenario.Options).CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(1, calls);
            Assert.Single(scenario.Store.Reservations);
            Assert.Equal(ExecutionCertainty.Unknown, (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))?.Certainty);
        }
        finally { finish.TrySetResult(DispatchScenario.Success(scenario.Session.Binding!)); }
    }

    /// <summary>Without an acknowledged operation ID the runtime cannot invent a target for native cancellation.</summary>
    [Fact]
    public async Task DoesNotCancelAnUnacknowledgedNativeOperation()
    {
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native", null),
            Capabilities = new(true, true, true, true, false),
            Sending = async (call, binding, observe, token) => await finish.Task,
            Cancelling = (context, operation, token) => { calls++; throw new InvalidOperationException("No native target is known."); },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Task<bool> pending = scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await new RequestAdmission(scenario.Store, scenario.Options).CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(0, calls);
            Assert.Single(scenario.Store.Reservations);
        }
        finally { finish.TrySetResult(DispatchScenario.Success(scenario.Session.Binding!)); }
    }
}
