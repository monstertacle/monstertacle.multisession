using System.Net;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Monstertacle.Multisession.Providers.Interactions;
using Xunit;
using ConversationData = Monstertacle.Multisession.ContractTests.Fixtures.ConversationFixtureData;
using InteractionData = Monstertacle.Multisession.ContractTests.Fixtures.InteractionFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Native continuation evidence stays independent of external end and local text success.</summary>
public sealed class ContinuationEvidenceTests
{
    /// <summary>A first safely ended interaction without a completed head cannot accept another turn.</summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("incomplete")]
    [InlineData("cancelled")]
    public async Task ReportsMissingFirstCompletedHead(string status)
    {
        JsonNode body = JsonNode.Parse(InteractionData.Json("root_completed"))!;
        body["status"] = status;
        body["steps"] = new JsonArray();
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(InteractionData.Call(), InteractionData.Lazy(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal(false, reply.ContinuationAvailable);
        Assert.Equal("interaction_root", reply.Binding?.SessionReference);
        Assert.Null(reply.Binding?.ContinuationState);
    }

    /// <summary>A confirmed new head remains usable even when local output capture exceeds its limit.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public async Task ReportsConfirmedHeadDespiteLocalOutputOutcome(int limit)
    {
        JsonNode body = JsonNode.Parse(InteractionData.Json("new_completed"))!;
        body["steps"]![0]!["content"]![0]!["text"] = "\U0001F642";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(InteractionData.Call(outputBytes: limit), InteractionData.Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(true, reply.ContinuationAvailable);
        Assert.Equal("interaction_next", reply.Binding?.ContinuationState);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
    }

    /// <summary>A client action the adapter cannot perform cannot leave its Conversation silently available.</summary>
    [Fact]
    public async Task ReportsUnsupportedCompletedClientActionAsUnavailable()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(ConversationData.Json("requires_client_action"))));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(ConversationData.Call(), ConversationData.Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.UnsupportedProviderAction, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal(false, reply.ContinuationAvailable);
    }

    /// <summary>Expired native references explicitly prevent continuation for either native protocol.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReportsNativeExpiry(bool conversation)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("{}", HttpStatusCode.NotFound)));
        ISessionProvider provider = conversation
            ? new ConversationSessionProvider(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler)
            : new InteractionSessionProvider(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        using IDisposable lifetime = (IDisposable)provider;
        ProviderReply reply = await provider.SendAsync(conversation ? ConversationData.Call() : InteractionData.Call(),
            conversation ? ConversationData.Binding() : InteractionData.Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderSessionExpired, reply.Error?.Code);
        Assert.Equal(false, reply.ContinuationAvailable);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
    }

    /// <summary>Known complete Conversation execution remains continuable after a local text-limit failure.</summary>
    [Fact]
    public async Task KeepsStableConversationAvailableAfterOutputLimit()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(ConversationData.Json("completed"))));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(ConversationData.Call(outputBytes: 1), ConversationData.Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.OutputLimitExceeded, reply.Error?.Code);
        Assert.Equal(true, reply.ContinuationAvailable);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
    }
}
