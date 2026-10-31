using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Failure certainty and continuation safety at framework boundaries.</summary>
public sealed class FrameworkFailureTests
{
    /// <summary>Invocation exceptions do not infer termination from an exception message.</summary>
    [Fact]
    public async Task InvocationFailureIsUnknown()
    {
        FrameworkTestAgent agent = new() { Running = _ => throw new InvalidOperationException("secret") };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default), FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ExecutionFailed, reply.Error?.Code);
        Assert.Empty(reply.Output);
        Assert.False(reply.ContinuationAvailable);
    }

    /// <summary>Unsupported operations never invoke or construct an agent.</summary>
    [Fact]
    public async Task UnsupportedActionsRemainUnknownAndDoNotInvoke()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        Assert.False(provider.Capabilities.SupportsInspection);
        Assert.False(provider.Capabilities.SupportsCancellation);
        Assert.True(provider.Capabilities.RequiresOperationAwareRecovery);
        foreach (ProviderReply reply in new[] { await provider.InspectAsync(FrameworkSessionProviderTests.Context(), "operation", default), await provider.CancelAsync(FrameworkSessionProviderTests.Context(), "operation", default) })
        {
            Assert.Equal(ErrorCode.UnsupportedProviderAction, reply.Error?.Code);
            Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        }
        Assert.Empty(agent.Inputs);
        Assert.Equal(0, agent.Creates);
    }

    /// <summary>Malformed, foreign or missing continuation is not repaired by creating a new session.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("not-json")]
    public async Task InvalidContinuationFailsBeforeRun(string? state)
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding binding = new("framework-contract-v1", 1, context.SessionId.ToString("N"), state);
        await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAsync(context, binding, default).AsTask());
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), binding, FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.False(reply.ContinuationAvailable);
        Assert.Empty(agent.Inputs);
        Assert.Equal(0, agent.Creates);
    }

    /// <summary>Missing terminal reason, truncation and tool/pending shapes cannot become complete success.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("length")]
    [InlineData("content_filter")]
    [InlineData("tool_calls")]
    public async Task IncompleteResponseIsUnknown(string? reason)
    {
        FrameworkTestAgent agent = new() { Running = _ => Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "partial")) { FinishReason = reason is null ? null : new ChatFinishReason(reason) }) };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default), FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.False(reply.OutputComplete);
        Assert.Empty(reply.Output);
    }

    /// <summary>Known finality survives unavailable serialized continuation and later turns are blocked.</summary>
    [Fact]
    public async Task SerializationFailurePreservesTerminationAndBlocksContinuation()
    {
        FrameworkTestAgent agent = new() { FailSerialization = true };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), binding, FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(RequestState.Failed, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal(binding, reply.Binding);
        Assert.False(reply.ContinuationAvailable);
    }

    /// <summary>Output limits reject the text without discarding confirmed continuation.</summary>
    [Fact]
    public async Task OutputLimitKeepsAdvancedState()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding binding = await provider.OpenAsync(context, default);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context, maximumBytes: 1), binding, FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(ErrorCode.OutputLimitExceeded, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.NotEqual(binding.ContinuationState, reply.Binding?.ContinuationState);
        Assert.Empty(reply.Output);
    }

    /// <summary>Failed durable observation does not dispatch the invocation.</summary>
    [Fact]
    public async Task FailedAcknowledgementDoesNotInvoke()
    {
        FrameworkTestAgent agent = new();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default), (_, _) => throw new IOException("private"), default);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.Empty(agent.Inputs);
    }
}
