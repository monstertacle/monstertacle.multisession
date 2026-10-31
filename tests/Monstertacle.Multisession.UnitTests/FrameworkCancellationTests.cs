using Microsoft.Agents.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Finite local waiting does not fabricate external termination.</summary>
public sealed class FrameworkCancellationTests
{
    /// <summary>An agent ignoring cancellation cannot hold the adapter caller forever.</summary>
    [Fact]
    public async Task DeadlineReturnsUnknownEvenWhenAgentIgnoresCancellation()
    {
        TaskCompletionSource<AgentResponse> external = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FrameworkTestAgent agent = new() { Running = _ => external.Task };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        ProviderCall call = FrameworkSessionProviderTests.Call(context) with { Timeout = TimeSpan.FromMilliseconds(30) };
        Task<ProviderReply> running = provider.SendAsync(call, binding, FrameworkSessionProviderTests.Observe, default).AsTask();
        try
        {
            ProviderReply result = await running.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(ErrorCode.TimedOut, result.Error?.Code);
            Assert.Equal(ExecutionCertainty.Unknown, result.Certainty);
            Assert.Null(result.Binding);
            Assert.Single(agent.Inputs);
        }
        finally { external.TrySetException(new IOException("late-private-failure")); }
    }

    /// <summary>Cancellation before dispatch performs no new agent run.</summary>
    [Fact]
    public async Task PreCancelledInvocationNeverRunsAgent()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        using CancellationTokenSource stopped = new();
        stopped.Cancel();
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), binding, FrameworkSessionProviderTests.Observe, stopped.Token);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.Equal(ErrorCode.Cancelled, reply.Error?.Code);
        Assert.NotEqual(false, reply.ContinuationAvailable);
        Assert.Empty(agent.Inputs);
    }
}
