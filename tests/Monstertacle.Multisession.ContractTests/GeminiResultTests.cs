using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.GeminiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Native terminal evidence and local text capture do not share the same continuation rules.</summary>
public sealed class GeminiResultTests
{
    /// <summary>Only completed text succeeds; pending client actions never advance the confirmed head.</summary>
    [Theory]
    [InlineData("failed", RequestState.Failed, ExecutionCertainty.ConfirmedEnded, ErrorCode.ExecutionFailed)]
    [InlineData("incomplete", RequestState.Failed, ExecutionCertainty.ConfirmedEnded, ErrorCode.ExecutionFailed)]
    [InlineData("cancelled", RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, ErrorCode.Cancelled)]
    [InlineData("requires_action", RequestState.Failed, ExecutionCertainty.Unknown, ErrorCode.UnsupportedProviderAction)]
    [InlineData("requires_continuation", RequestState.Failed, ExecutionCertainty.Unknown, ErrorCode.UnsupportedProviderAction)]
    [InlineData("unknown-status", RequestState.Failed, ExecutionCertainty.Unknown, ErrorCode.ProviderUnavailable)]
    public async Task DistinguishesOutcomeAndConfirmedHead(string status, RequestState outcome, ExecutionCertainty certainty, ErrorCode code)
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["status"] = status;
        body["steps"] = new JsonArray();
        ProviderReply reply = await SendAsync(body.ToJsonString(), Binding());
        Assert.Equal(outcome, reply.Outcome);
        Assert.Equal(certainty, reply.Certainty);
        Assert.Equal(code, reply.Error?.Code);
        Assert.Empty(reply.Output);
        Assert.False(reply.OutputComplete);
        Assert.Equal(certainty == ExecutionCertainty.ConfirmedEnded ? Binding() : null, reply.Binding);
    }

    /// <summary>Output overflow does not permit a subsequent turn to branch from the obsolete head.</summary>
    [Theory]
    [InlineData(4, RequestState.Succeeded)]
    [InlineData(3, RequestState.Failed)]
    public async Task PreservesConfirmedNewHeadAcrossInclusiveUtf8Boundary(int limit, RequestState outcome)
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["steps"]![0]!["content"]![0]!["text"] = "\U0001F642";
        ProviderReply reply = await SendAsync(body.ToJsonString(), Binding(), limit);
        Assert.Equal(outcome, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal("interaction_root", reply.Binding?.SessionReference);
        Assert.Equal("interaction_next", reply.Binding?.ContinuationState);
        Assert.Equal(outcome == RequestState.Succeeded, reply.OutputComplete);
        Assert.Equal(outcome == RequestState.Succeeded ? "\U0001F642" : string.Empty, reply.Output);
        Assert.Equal(outcome == RequestState.Succeeded ? null : ErrorCode.OutputLimitExceeded, reply.Error?.Code);
    }

    /// <summary>Completed native execution without text is a local failure while its confirmed continuation remains known.</summary>
    [Fact]
    public async Task RejectsEmptyOutputWithoutLosingNativeHead()
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["steps"] = new JsonArray();
        ProviderReply reply = await SendAsync(body.ToJsonString(), Binding());
        Assert.Equal(RequestState.Failed, reply.Outcome);
        Assert.Equal(ErrorCode.ExecutionFailed, reply.Error?.Code);
        Assert.False(reply.OutputComplete);
        Assert.Equal("interaction_next", reply.Binding?.ContinuationState);
    }

    /// <summary>Explicit contradictory predecessor evidence cannot publish a new continuation.</summary>
    [Fact]
    public async Task RejectsForeignPreviousInteraction()
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["previous_interaction_id"] = "foreign-head";
        ProviderReply reply = await SendAsync(body.ToJsonString(), Binding());
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Null(reply.Binding);
    }

    /// <summary>The old head's identity is not fresh evidence for a new execution.</summary>
    [Fact]
    public async Task RejectsReusedConfirmedHeadIdentity()
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["id"] = Binding().ContinuationState;
        ProviderReply reply = await SendAsync(body.ToJsonString(), Binding());
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Null(reply.Binding);
    }

    /// <summary>First failure may leave a known root without a confirmed head; it cannot silently become another lazy session.</summary>
    [Fact]
    public async Task RefusesNewRootAfterFailedFirstExecution()
    {
        JsonNode failed = JsonNode.Parse(Json("root_completed"))!;
        failed["status"] = "failed";
        failed["steps"] = new JsonArray();
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(failed.ToJsonString())));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply first = await provider.SendAsync(Call(), Lazy(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(RequestState.Failed, first.Outcome);
        Assert.Equal("interaction_root", first.Binding?.SessionReference);
        Assert.Null(first.Binding?.ContinuationState);
        Assert.NotNull(first.Binding);
        ProviderReply second = await provider.SendAsync(Call("following input"), first.Binding, (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.NotStarted, second.Certainty);
        Assert.Equal(ErrorCode.ProviderSessionExpired, second.Error?.Code);
        Assert.Equal(1, handler.Calls);
    }

    private static async Task<ProviderReply> SendAsync(string body, ProviderBinding binding, int limit = 1024)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body)));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        return await provider.SendAsync(Call(outputBytes: limit), binding, (item, token) => ValueTask.CompletedTask, CancellationToken.None);
    }
}
