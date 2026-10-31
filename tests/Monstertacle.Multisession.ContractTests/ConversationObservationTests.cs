using System.Net;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.ConversationFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks explicit native read/cancel evidence without implicit send or local store resolution.</summary>
public sealed class ConversationObservationTests
{
    /// <summary>Inspection is one GET and preserves both pending and terminal native evidence.</summary>
    [Theory]
    [InlineData("queued", RequestState.Running, ExecutionCertainty.InProgress)]
    [InlineData("completed", RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded)]
    public async Task InspectsWithoutStartingOrResolvingAnything(string fixture, RequestState state, ExecutionCertainty certainty)
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/responses/resp_fixture", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(Json(fixture)));
        });
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.InspectAsync(Context(), "resp_fixture", CancellationToken.None);
        Assert.Equal(state, reply.Outcome);
        Assert.Equal(certainty, reply.Certainty);
        Assert.Null(reply.Binding);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Cancel acknowledgement is followed by a native end observation rather than being treated as termination.</summary>
    [Fact]
    public async Task CancelsOnceAndConfirmsEndByGet()
    {
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/v1/responses/resp_fixture/cancel", request.RequestUri?.AbsolutePath);
                Assert.Equal(1, credentials.Calls);
                return Task.FromResult(ScriptedHttpHandler.Json(Json("queued")));
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/responses/resp_fixture", request.RequestUri?.AbsolutePath);
            Assert.Equal(2, credentials.Calls);
            JsonNode cancelled = JsonNode.Parse(Json("queued"))!;
            cancelled["status"] = "cancelled";
            return Task.FromResult(ScriptedHttpHandler.Json(cancelled.ToJsonString()));
        });
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, credentials, handler);
        ProviderReply reply = await provider.CancelAsync(Context(), "resp_fixture", CancellationToken.None);
        Assert.Equal(RequestState.Cancelled, reply.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
        Assert.Equal(2, handler.Calls);
    }

    /// <summary>Expired operations provide no current end evidence and are never recreated.</summary>
    [Fact]
    public async Task ReportsExpiredInspectionAsUnknown()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("{}", HttpStatusCode.NotFound)));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.InspectAsync(Context(), "resp_fixture", CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderSessionExpired, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>An operation read must match the explicitly requested identity.</summary>
    [Fact]
    public async Task RejectsOtherOperationInInspection()
    {
        JsonNode body = JsonNode.Parse(Json("completed"))!;
        body["id"] = "resp_other";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.InspectAsync(Context(), "resp_fixture", CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderUnavailable, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Empty(reply.Output);
    }

    /// <summary>No network request can be addressed by an absent native identity.</summary>
    [Fact]
    public async Task RejectsEmptyOperationWithoutHttp()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("completed"))));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.InspectAsync(Context(), " ", CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }
}
