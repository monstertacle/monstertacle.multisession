using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Request-linked native evidence and typed outcomes on actual SQLite, across store reopening.</summary>
public sealed class FrameworkEvidenceTests
{
    private static Func<AgentRunOptions?, CancellationToken, Task<AgentResponse>> Observing(string native, string? operation, Func<AgentResponse> result)
        => async (options, token) =>
        {
            Assert.True(FrameworkInvocation.TryGet(options, out FrameworkInvocation? invocation));
            await invocation!.ObserveNativeSessionAsync(native, token);
            if (operation is not null) { await invocation.ObserveOperationAcceptedAsync(operation, token); }
            return result();
        };

    private static AgentResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Stop };

    /// <summary>Native session and accepted operation persist as request evidence without replacing the stable SDK binding.</summary>
    [Fact]
    public async Task NativeEvidenceSurvivesReopenWithoutReplacingBinding()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new() { RunningWith = Observing("native-x", "operation-x", () => Text("done")) };
        (Guid session, Guid request, RequestSnapshot ended) = await FrameworkEvidenceScenario.RunOnceAsync(directory, agent);
        Assert.Equal(RequestState.Succeeded, ended.State);
        ExecutionEvidence expected = new(true, "operation-x", "native-x");
        Assert.Equal(expected, ended.Evidence);
        (RequestSnapshot? reopened, SessionSnapshot? snapshot) = await FrameworkEvidenceScenario.ReopenAsync(directory, agent, session, request);
        Assert.Equal(expected, reopened?.Evidence);
        Assert.Equal(session.ToString("N"), snapshot?.Binding?.SessionReference);
        Assert.Single(agent.Inputs);
    }

    /// <summary>A known native failure persists its partial output, releases capacity and leaves the session usable.</summary>
    [Fact]
    public async Task KnownFailureKeepsPartialOutputAndReleasesCapacity()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new()
        {
            RunningWith = Observing("native-f", null, () => new AgentResponse(new ChatMessage(ChatRole.Assistant, string.Empty))
            {
                FinishReason = ChatFinishReason.Stop,
                RawRepresentation = new FrameworkExecutionOutcome
                {
                    State = RequestState.Failed,
                    Certainty = ExecutionCertainty.ConfirmedEnded,
                    Output = "partial",
                    ErrorCode = ErrorCode.ExecutionFailed,
                    ContinuationAvailable = true,
                },
            }),
        };
        (Guid session, Guid request, RequestSnapshot ended) = await FrameworkEvidenceScenario.RunOnceAsync(directory, agent);
        Assert.Equal((RequestState.Failed, ExecutionCertainty.ConfirmedEnded), (ended.State, ended.Certainty));
        Assert.Equal(("partial", false), (ended.Result?.Output, ended.Result?.OutputComplete));
        Assert.Equal(new ExecutionEvidence(true, null, "native-f"), ended.Evidence);
        (_, SessionSnapshot? snapshot) = await FrameworkEvidenceScenario.ReopenAsync(directory, agent, session, request);
        Assert.Equal(SessionBlockReason.None, snapshot?.BlockReason);
    }

    /// <summary>An uncertain invocation keeps its native evidence after restart and never becomes not-invoked.</summary>
    [Fact]
    public async Task UncertainInvocationKeepsEvidenceAfterRestart()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new() { RunningWith = Observing("native-u", null, () => throw new IOException("private")) };
        (Guid session, Guid request, RequestSnapshot ended) = await FrameworkEvidenceScenario.RunOnceAsync(directory, agent);
        Assert.Equal(ExecutionCertainty.Unknown, ended.Certainty);
        (RequestSnapshot? reopened, SessionSnapshot? snapshot) = await FrameworkEvidenceScenario.ReopenAsync(directory, agent, session, request);
        Assert.Equal(ExecutionCertainty.Unknown, reopened?.Certainty);
        Assert.Equal(new ExecutionEvidence(true, null, "native-u"), reopened?.Evidence);
        Assert.Equal(SessionBlockReason.RecoveryRequired, snapshot?.BlockReason);
        Assert.Single(agent.Inputs);
    }

    /// <summary>A conflicting second native identity is refused durably and cannot be overtaken by success.</summary>
    [Fact]
    public async Task ConflictingNativeIdentityIsUncertain()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                Assert.True(FrameworkInvocation.TryGet(options, out FrameworkInvocation? invocation));
                await invocation!.ObserveNativeSessionAsync("first", token);
                try { await invocation.ObserveNativeSessionAsync("second", token); }
                catch (SessionOperationException) { }
                return Text("done");
            },
        };
        (_, _, RequestSnapshot ended) = await FrameworkEvidenceScenario.RunOnceAsync(directory, agent);
        Assert.Equal(ExecutionCertainty.Unknown, ended.Certainty);
        Assert.Equal("first", ended.Evidence?.NativeSessionReference);
    }
}
