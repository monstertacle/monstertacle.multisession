using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class FrameworkTestAgent : AIAgent
{
    protected override string? IdCore => "qualified-test-agent";
    internal List<string> Inputs { get; } = [];
    internal Func<CancellationToken, Task<AgentResponse>>? Running { get; set; }
    internal Func<AgentRunOptions?, CancellationToken, Task<AgentResponse>>? RunningWith { get; set; }
    internal Func<CancellationToken, ValueTask>? BeforeRestore { get; set; }
    internal Action? AfterTurn { get; set; }
    internal bool FailSerialization { get; set; }
    internal bool FailAllSerialization { get; set; }
    internal int Creates { get; private set; }
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        Creates++;
        return ValueTask.FromResult<AgentSession>(new FrameworkTestSession());
    }
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        if (FailAllSerialization || FailSerialization && ((FrameworkTestSession)session).Turns > 0) { throw new InvalidOperationException("private-detail"); }
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(((FrameworkTestSession)session).Turns));
    }
    protected override async ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        if (BeforeRestore is { } restore) { await restore(cancellationToken); }
        return new FrameworkTestSession { Turns = serializedState.GetInt32() };
    }
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        Inputs.Add(Assert.Single(messages).Text);
        if (RunningWith is not null) { return await RunningWith(options, cancellationToken); }
        if (Running is not null) { return await Running(cancellationToken); }
        int turn = ++((FrameworkTestSession)session!).Turns;
        AfterTurn?.Invoke();
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, $"turn-{turn}")) { FinishReason = ChatFinishReason.Stop };
    }
    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
