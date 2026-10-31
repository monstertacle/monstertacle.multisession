using System.Collections.Frozen;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Framework session identity, independent restoration and durable observation ordering.</summary>
public sealed class FrameworkSessionProviderTests
{
    internal static ProviderSessionContext Context() => new(Guid.NewGuid(), "host-model", ExecutionKind.AgentTask, new(null, null), FrozenDictionary<string, string>.Empty);
    internal static FrameworkSessionProvider Provider(FrameworkTestAgent agent, FrameworkAgentOptions? options = null)
        => new(options ?? new() { AdapterId = "framework-contract-v1" }, _ => agent);
    internal static ProviderCall Call(ProviderSessionContext context, string input = "new input", int maximumBytes = 1000)
        => new(context, Guid.NewGuid(), Guid.NewGuid(), input, TimeSpan.FromSeconds(5), maximumBytes);
    internal static ValueTask Observe(ProviderObservation observation, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>New adapter instances restore confirmed state and never resubmit previous inputs.</summary>
    [Fact]
    public async Task RecreatesAdapterAndContinuesWithoutReplay()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = Provider(agent);
        ProviderSessionContext context = Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        ProviderReply first = await provider.SendAsync(Call(context, "first"), binding, Observe, default);
        Assert.Equal(RequestState.Succeeded, first.Outcome);
        Assert.Equal("turn-1", first.Output);
        FrameworkSessionProvider recreated = Provider(agent);
        Assert.Equal(first.Binding, await recreated.ReopenAsync(context, first.Binding!, default));
        ProviderReply second = await recreated.SendAsync(Call(context, "second"), first.Binding!, Observe, default);
        Assert.Equal("turn-2", second.Output);
        Assert.Equal(binding.SessionReference, second.Binding?.SessionReference);
        Assert.Equal(["first", "second"], agent.Inputs);
        Assert.Equal(1, agent.Creates);
    }

    /// <summary>External invocation waits for the durable intent observation acknowledgement.</summary>
    [Fact]
    public async Task AwaitsObservationBeforeInvocation()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = Provider(agent);
        ProviderSessionContext context = Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        TaskCompletionSource acknowledgement = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource seen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ProviderReply> running = provider.SendAsync(Call(context), binding, async (observation, token) =>
        {
            Assert.Equal(ProviderObservationKind.InvocationPrepared, observation.Kind);
            seen.SetResult();
            await acknowledgement.Task.WaitAsync(token);
        }, default).AsTask();
        Task winner = await Task.WhenAny(seen.Task, running).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(seen.Task, winner);
        Assert.Empty(agent.Inputs);
        acknowledgement.SetResult();
        Assert.Equal(RequestState.Succeeded, (await running).Outcome);
        Assert.Single(agent.Inputs);
    }

    /// <summary>Only the final assistant message is returned, excluding completed intermediate messages.</summary>
    [Fact]
    public async Task SelectsFinalAssistantText()
    {
        FrameworkTestAgent agent = new()
        {
            Running = _ => Task.FromResult(new AgentResponse
            {
                Messages = [new(ChatRole.Tool, "intermediate"), new(ChatRole.Assistant, "final")],
                FinishReason = ChatFinishReason.Stop,
            }),
        };
        FrameworkSessionProvider provider = Provider(agent);
        ProviderSessionContext context = Context();
        ProviderReply reply = await provider.SendAsync(Call(context), await provider.OpenAsync(context, default), Observe, default);
        Assert.Equal("final", reply.Output);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
    }
}
