using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.OpenAI;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.OpenAiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Exercises explicit monotonic deadlines against uncooperative fixture waits, without sleeps.</summary>
public sealed class OpenAiTransportBoundaryTests
{
    /// <summary>A header wait is limited to thirty seconds even when a handler ignores its token.</summary>
    [Fact]
    public async Task BoundsHeaderWaitWithoutRetry()
    {
        using HttpTestClock clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<HttpResponseMessage> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((request, token) => { entered.TrySetResult(); return release.Task; });
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler, clock);
        Task<ProviderBinding> pending = provider.OpenAsync(Context(), CancellationToken.None).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Fire(TimeSpan.FromSeconds(30));
            SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.ProviderUnavailable, error.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, error.Failure.Certainty);
            Assert.Equal(1, handler.Calls);
        }
        finally { release.TrySetResult(ScriptedHttpHandler.Json(Json("conversation"))); }
    }

    /// <summary>The total dispatch timeout applies before headers and never proves external termination.</summary>
    [Fact]
    public async Task BoundsEntireSendByCallDeadline()
    {
        using HttpTestClock clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<HttpResponseMessage> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((request, token) => { entered.TrySetResult(); return release.Task; });
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler, clock);
        ProviderCall call = Call() with { Timeout = TimeSpan.FromSeconds(2) };
        Task<ProviderReply> pending = provider.SendAsync(call, Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Fire(call.Timeout);
            ProviderReply reply = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(RequestState.TimedOut, reply.Outcome);
            Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
            Assert.Equal(ErrorCode.TimedOut, reply.Error?.Code);
            Assert.Equal(1, handler.Calls);
        }
        finally { release.TrySetResult(ScriptedHttpHandler.Json(Json("completed"))); }
    }

    /// <summary>A callback that ignores cancellation cannot bypass the adapter's total deadline.</summary>
    [Fact]
    public async Task BoundsUncooperativeObservationCallback()
    {
        using HttpTestClock clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("completed"))));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler, clock);
        ProviderCall call = Call() with { Timeout = TimeSpan.FromSeconds(2) };
        Task<ProviderReply> pending = provider.SendAsync(call, Binding(), async (item, token) => { entered.TrySetResult(); await release.Task; }, CancellationToken.None).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Fire(call.Timeout);
            ProviderReply reply = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
            Assert.Equal(ErrorCode.TimedOut, reply.Error?.Code);
            Assert.Equal(1, handler.Calls);
        }
        finally { release.TrySetResult(); }
    }

    /// <summary>Native JSON metadata has a separate finite limit and cannot yield truncated successful output.</summary>
    [Fact]
    public async Task RejectsOversizedNativeBodyConservatively()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("completed"))));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference", MaximumResponseBytes = 64 }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.OutputLimitExceeded, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Empty(reply.Output);
        Assert.False(reply.OutputComplete);
    }
}
