using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Interactions;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.InteractionFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Explicit recovery checks the uncertain execution, not merely its old confirmed predecessor.</summary>
public sealed class InteractionRecoveryTests
{
    /// <summary>One read of the known completed operation verifies the new head while retaining the first identity.</summary>
    [Fact]
    public async Task RecoversCompletedUncertainOperationIntoNewHead()
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/interactions/interaction_next", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(Json("new_completed")));
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderBinding recovered = await provider.ReopenAfterExecutionAsync(Context(), Binding(), "interaction_next", CancellationToken.None);
        Assert.Equal("interaction_root", recovered.SessionReference);
        Assert.Equal("interaction_next", recovered.ContinuationState);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>A known first native identity can be checked without creating another one or reusing an empty lazy binding.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("interaction_root")]
    public async Task RecoversFirstPersistedNativeIdentity(string? operation)
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/interactions/interaction_root", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(Json("root_completed")));
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderBinding known = new("gemini-interactions", 1, "interaction_root", null);
        ProviderBinding recovered = await provider.ReopenAfterExecutionAsync(Context(), known, operation, CancellationToken.None);
        Assert.Equal("interaction_root", recovered.SessionReference);
        Assert.Equal("interaction_root", recovered.ContinuationState);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>An old head or a completely lazy binding supplies no evidence for the uncertain new execution.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsMissingNewOperationOrUnknownFirstIdentity(bool unknownFirst)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("head_completed"))));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAfterExecutionAsync(
            Context(), unknownFirst ? Lazy() : Binding(), unknownFirst ? "interaction_next" : null, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedProviderAction, error.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>Pending or client-dependent native execution cannot be bypassed by reading the old head.</summary>
    [Theory]
    [InlineData("in_progress")]
    [InlineData("requires_action")]
    [InlineData("requires_continuation")]
    public async Task RejectsNativeExecutionThatStillRequiresWork(string status)
    {
        JsonNode body = JsonNode.Parse(Json("new_in_progress"))!;
        body["status"] = status;
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAfterExecutionAsync(Context(), Binding(), "interaction_next", CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedProviderAction, error.Failure.Code);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>A confirmed failed execution may retain its old completed head only after both native reads are verified.</summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("incomplete")]
    [InlineData("cancelled")]
    public async Task RevalidatesOldHeadAfterKnownNativeFailure(string status)
    {
        int reads = 0;
        JsonNode failed = JsonNode.Parse(Json("new_completed"))!;
        failed["status"] = status;
        failed["steps"] = new JsonArray();
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(reads++ == 0 ? "/v1/interactions/interaction_next" : "/v1/interactions/interaction_head", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(reads == 1 ? failed.ToJsonString() : Json("head_completed")));
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        Assert.Equal(Binding(), await provider.ReopenAfterExecutionAsync(Context(), Binding(), "interaction_next", CancellationToken.None));
        Assert.Equal(2, reads);
    }

    /// <summary>A completed resource from a contradictory predecessor cannot be adopted as the new head.</summary>
    [Fact]
    public async Task RejectsForeignLineageDuringRecovery()
    {
        JsonNode body = JsonNode.Parse(Json("new_completed"))!;
        body["previous_interaction_id"] = "foreign-head";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAfterExecutionAsync(Context(), Binding(), "interaction_next", CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderUnavailable, error.Failure.Code);
        Assert.Equal(1, handler.Calls);
    }
}
