using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.ConversationFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks terminal evidence, native errors and unmodified complete text boundaries.</summary>
public sealed class ConversationResultTests
{
    /// <summary>HTTP success alone does not prove a successful or even ended external execution.</summary>
    [Theory]
    [InlineData("failed", RequestState.Failed, ExecutionCertainty.ConfirmedEnded, ErrorCode.ExecutionFailed)]
    [InlineData("incomplete", RequestState.Failed, ExecutionCertainty.ConfirmedEnded, ErrorCode.ExecutionFailed)]
    [InlineData("cancelled", RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, ErrorCode.Cancelled)]
    [InlineData("requires_action", RequestState.Failed, ExecutionCertainty.Unknown, ErrorCode.UnsupportedProviderAction)]
    [InlineData("unknown-state", RequestState.Failed, ExecutionCertainty.Unknown, ErrorCode.ProviderUnavailable)]
    [InlineData("completed", RequestState.Failed, ExecutionCertainty.ConfirmedEnded, ErrorCode.ExecutionFailed)]
    public async Task DistinguishesNativeStatusFromSuccessfulText(string status, RequestState outcome, ExecutionCertainty certainty, ErrorCode code)
    {
        string body = new JsonObject { ["id"] = "resp_fixture", ["object"] = "response", ["status"] = status, ["output"] = new JsonArray() }.ToJsonString();
        ProviderReply reply = await SendAsync(body);
        Assert.Equal(outcome, reply.Outcome);
        Assert.Equal(certainty, reply.Certainty);
        Assert.Equal(code, reply.Error?.Code);
        Assert.Empty(reply.Output);
        Assert.False(reply.OutputComplete);
    }

    /// <summary>Never executes client-side action or invents substitute text for it.</summary>
    [Fact]
    public async Task RejectsCompletedClientAction()
    {
        ProviderReply reply = await SendAsync(Json("requires_client_action"));
        Assert.Equal(RequestState.Failed, reply.Outcome);
        Assert.Equal(ErrorCode.UnsupportedProviderAction, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Empty(reply.Output);
    }

    /// <summary>The inclusive byte boundary preserves complete text and rejects excess without truncation.</summary>
    [Theory]
    [InlineData(4, RequestState.Succeeded)]
    [InlineData(3, RequestState.Failed)]
    public async Task AppliesInclusiveUtf8OutputLimit(int maximum, RequestState outcome)
    {
        JsonNode body = JsonNode.Parse(Json("completed"))!;
        body["output"]![0]!["content"]![0]!["text"] = "\U0001F642";
        ProviderReply reply = await SendAsync(body.ToJsonString(), maximum);
        Assert.Equal(outcome, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal(outcome == RequestState.Succeeded, reply.OutputComplete);
        Assert.Equal(outcome == RequestState.Succeeded ? "\U0001F642" : string.Empty, reply.Output);
        Assert.Equal(outcome == RequestState.Succeeded ? null : ErrorCode.OutputLimitExceeded, reply.Error?.Code);
    }

    /// <summary>Known public provider codes map to categories and never expose arbitrary provider messages.</summary>
    [Fact]
    public async Task MapsNativeContextLimitWithoutLeakingMessage()
    {
        ProviderReply reply = await SendAsync("""{"id":"resp_fixture","object":"response","status":"failed","output":[],"error":{"code":"context_length_exceeded","message":"private provider details"}}""");
        Assert.Equal(ErrorCode.ProviderContextLimit, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.DoesNotContain("private", reply.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Missing operation identity is unconfirmed and never sent into the persistence callback.</summary>
    [Fact]
    public async Task RequiresOperationIdentityBeforeAcknowledgement()
    {
        int observations = 0;
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("""{"object":"response","status":"completed","output":[]}""")));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => { observations++; return ValueTask.CompletedTask; }, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Equal(0, observations);
    }

    /// <summary>A poll for another operation must not publish its output under the reserved request.</summary>
    [Fact]
    public async Task RejectsMismatchedPollIdentity()
    {
        JsonNode other = JsonNode.Parse(Json("completed"))!;
        other["id"] = "resp_other";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(request.Method == HttpMethod.Post ? Json("queued") : other.ToJsonString())));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Empty(reply.Output);
    }

    private static async Task<ProviderReply> SendAsync(string body, int maximumOutput = 1024)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body)));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        return await provider.SendAsync(Call(outputBytes: maximumOutput), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
    }
}
