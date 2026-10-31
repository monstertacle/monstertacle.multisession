using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Qualified typed native outcomes, independent from text, continuation and output completeness.</summary>
public sealed class FrameworkOutcomeTests
{
    private static readonly FrameworkAgentOptions _qualified = new() { AdapterId = "framework-contract-v1", QualifiedOutcomes = true };

    private static FrameworkTestAgent Returning(object? raw, string text = "ok") => new()
    {
        Running = _ => Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Stop, RawRepresentation = raw }),
    };

    private static async Task<(ProviderReply Reply, ProviderBinding Opened)> SendAsync(FrameworkTestAgent agent, FrameworkAgentOptions options)
    {
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent, options);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding opened = await provider.OpenAsync(context, default);
        return (await provider.SendAsync(FrameworkSessionProviderTests.Call(context), opened, FrameworkSessionProviderTests.Observe, default), opened);
    }

    /// <summary>A known native failure keeps its end, partial output and confirmed continuation.</summary>
    [Fact]
    public async Task KnownFailurePreservesPartialOutputAndEnd()
    {
        FrameworkExecutionOutcome outcome = new()
        {
            State = RequestState.Failed,
            Certainty = ExecutionCertainty.ConfirmedEnded,
            Output = "partial",
            OutputComplete = false,
            ContinuationAvailable = true,
            ErrorCode = ErrorCode.ExecutionFailed,
        };
        (ProviderReply reply, ProviderBinding opened) = await SendAsync(Returning(outcome, string.Empty), _qualified);
        Assert.Equal(RequestState.Failed, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal("partial", reply.Output);
        Assert.False(reply.OutputComplete);
        Assert.Equal(ErrorCode.ExecutionFailed, reply.Error?.Code);
        Assert.True(reply.ContinuationAvailable);
        Assert.NotNull(reply.Binding);
        Assert.Equal(opened.SessionReference, reply.Binding!.SessionReference);
    }

    /// <summary>A typed not-started fact keeps the previous continuation and reports no external execution.</summary>
    [Fact]
    public async Task NotStartedKeepsContinuation()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Failed, Certainty = ExecutionCertainty.NotStarted, ErrorCode = ErrorCode.ProviderUnavailable, ContinuationAvailable = true };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome), _qualified);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Null(reply.Binding);
        Assert.Null(reply.ContinuationAvailable);
        Assert.Empty(reply.Output);
    }

    /// <summary>A known end without usable continuation blocks later turns without erasing the result.</summary>
    [Fact]
    public async Task EndedWithoutContinuationBlocksLaterTurns()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Succeeded, Certainty = ExecutionCertainty.ConfirmedEnded, Output = "done", OutputComplete = true };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome, "different text"), _qualified);
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.Equal("done", reply.Output);
        Assert.Null(reply.Binding);
        Assert.False(reply.ContinuationAvailable);
    }

    /// <summary>Typed control facts from an unqualified registration fail closed instead of selecting text success.</summary>
    [Fact]
    public async Task UnqualifiedTypedOutcomeFailsClosed()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Succeeded, Certainty = ExecutionCertainty.ConfirmedEnded, Output = "ok", OutputComplete = true, ContinuationAvailable = true };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome), new() { AdapterId = "framework-contract-v1" });
        AssertUnknown(reply);
    }

    /// <summary>Inconsistent typed facts never fall back to the response text.</summary>
    [Theory]
    [InlineData(RequestState.Succeeded, ExecutionCertainty.Unknown, true, null)]
    [InlineData(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, false, null)]
    [InlineData(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, true, ErrorCode.ExecutionFailed)]
    [InlineData(RequestState.Failed, ExecutionCertainty.ConfirmedEnded, false, null)]
    [InlineData(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, false, ErrorCode.ExecutionFailed)]
    [InlineData(RequestState.TimedOut, ExecutionCertainty.ConfirmedEnded, false, ErrorCode.Cancelled)]
    [InlineData(RequestState.Running, ExecutionCertainty.InProgress, false, null)]
    [InlineData(RequestState.Interrupted, ExecutionCertainty.Unknown, false, ErrorCode.Interrupted)]
    [InlineData(RequestState.Failed, ExecutionCertainty.InProgress, false, ErrorCode.ExecutionFailed)]
    [InlineData(RequestState.Failed, (ExecutionCertainty)42, false, ErrorCode.ExecutionFailed)]
    [InlineData(RequestState.Failed, ExecutionCertainty.ConfirmedEnded, false, (ErrorCode)99)]
    public async Task InconsistentFactsFailClosed(RequestState state, ExecutionCertainty certainty, bool complete, ErrorCode? code)
    {
        FrameworkExecutionOutcome outcome = new() { State = state, Certainty = certainty, Output = "ok", OutputComplete = complete, ErrorCode = code, ContinuationAvailable = true };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome), _qualified);
        AssertUnknown(reply);
    }

    /// <summary>Unknown or not-started facts cannot claim continuation or captured output.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.Unknown, "", true)]
    [InlineData(ExecutionCertainty.NotStarted, "partial", false)]
    public async Task UncertainFactsCannotCarryEvidenceOfProgress(ExecutionCertainty certainty, string output, bool continuation)
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Failed, Certainty = certainty, Output = output, ErrorCode = ErrorCode.ExecutionFailed, ContinuationAvailable = continuation };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome), _qualified);
        AssertUnknown(reply);
    }

    /// <summary>A typed Unknown fact remains uncertain, never not-invoked.</summary>
    [Fact]
    public async Task TypedUnknownRemainsUnknown()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Failed, Certainty = ExecutionCertainty.Unknown, ErrorCode = ErrorCode.ProviderUnavailable };
        (ProviderReply reply, _) = await SendAsync(Returning(outcome), _qualified);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.False(reply.ContinuationAvailable);
        Assert.Null(reply.Binding);
    }

    /// <summary>Background continuation tokens are rejected even for otherwise valid typed facts.</summary>
    [Fact]
    public async Task BackgroundContinuationRejectsTypedFacts()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Succeeded, Certainty = ExecutionCertainty.ConfirmedEnded, Output = "ok", OutputComplete = true, ContinuationAvailable = true };
#pragma warning disable MEAI001
        FrameworkTestAgent agent = new()
        {
            Running = _ => Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                FinishReason = ChatFinishReason.Stop,
                RawRepresentation = outcome,
                ContinuationToken = ResponseContinuationToken.FromBytes(new byte[] { 1 }),
            }),
        };
#pragma warning restore MEAI001
        (ProviderReply reply, _) = await SendAsync(agent, _qualified);
        AssertUnknown(reply);
    }

    /// <summary>JSON lookalikes are ordinary text and never select typed control facts.</summary>
    [Fact]
    public async Task JsonLookalikeRemainsText()
    {
        FrameworkExecutionOutcome outcome = new() { State = RequestState.Failed, Certainty = ExecutionCertainty.NotStarted, ErrorCode = ErrorCode.ProviderUnavailable };
        string json = JsonSerializer.Serialize(outcome);
        (ProviderReply reply, _) = await SendAsync(Returning(JsonSerializer.SerializeToElement(outcome), json), _qualified);
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.Equal(json, reply.Output);
        Assert.True(reply.ContinuationAvailable);
    }

    /// <summary>Serialization failure after a typed known end keeps state and output but blocks continuation.</summary>
    [Fact]
    public async Task TypedEndSurvivesSerializationFailure()
    {
        FrameworkTestAgent agent = new();
        agent.RunningWith = (_, _) =>
        {
            return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, string.Empty))
            {
                FinishReason = ChatFinishReason.Stop,
                RawRepresentation = new FrameworkExecutionOutcome
                {
                    State = RequestState.Failed,
                    Certainty = ExecutionCertainty.ConfirmedEnded,
                    Output = "captured",
                    ErrorCode = ErrorCode.ExecutionFailed,
                    ContinuationAvailable = true,
                },
            });
        };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent, _qualified);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding opened = await provider.OpenAsync(context, default);
        agent.FailAllSerialization = true;
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), opened, FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(RequestState.Failed, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal("captured", reply.Output);
        Assert.Null(reply.Binding);
        Assert.False(reply.ContinuationAvailable);
    }

    private static void AssertUnknown(ProviderReply reply)
    {
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.NotEqual(RequestState.Succeeded, reply.Outcome);
        Assert.Empty(reply.Output);
        Assert.False(reply.OutputComplete);
        Assert.Null(reply.Binding);
        Assert.False(reply.ContinuationAvailable);
    }
}
