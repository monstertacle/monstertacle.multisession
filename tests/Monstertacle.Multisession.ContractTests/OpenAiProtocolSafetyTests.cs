using System.Net;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.OpenAI;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.OpenAiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Rejects invalid native evidence and preserves safe pre-execution error categories.</summary>
public sealed class OpenAiProtocolSafetyTests
{
    /// <summary>A known context rejection is distinguishable without exposing the arbitrary HTTP error message.</summary>
    [Fact]
    public async Task MapsContextRejectionBeforeNativeStart()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("""{"error":{"code":"context_length_exceeded","message":"private context details"}}""", HttpStatusCode.BadRequest)));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderContextLimit, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.DoesNotContain("private", reply.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>A whitespace identity cannot be persisted as evidence of a specific external operation.</summary>
    [Fact]
    public async Task RejectsWhitespaceOperationIdentity()
    {
        int observations = 0;
        JsonNode body = JsonNode.Parse(Json("completed"))!;
        body["id"] = " ";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => { observations++; return ValueTask.CompletedTask; }, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(0, observations);
        Assert.Empty(reply.Output);
    }

    /// <summary>Explicit contradictory conversation evidence cannot publish another session's successful text.</summary>
    [Fact]
    public async Task RejectsContradictoryConversationEvidence()
    {
        JsonNode body = JsonNode.Parse(Json("completed"))!;
        body["conversation"] = new JsonObject { ["id"] = "conv_other" };
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Empty(reply.Output);
    }

    /// <summary>Direct adapter consumers receive validation before any irreversible POST for invalid limits.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsInvalidCallLimitsBeforeHttp(bool timeout)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("completed"))));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderCall call = timeout ? Call() with { Timeout = Timeout.InfiniteTimeSpan } : Call(outputBytes: 0);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.SendAsync(call, Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, error.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, error.Failure.Certainty);
        Assert.Equal(0, handler.Calls);
    }
}
