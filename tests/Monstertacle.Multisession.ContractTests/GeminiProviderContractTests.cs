using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.GeminiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Stable v1 native identity contracts using synthetic local fixtures.</summary>
public sealed class GeminiProviderContractTests
{
    /// <summary>Open is explicitly lazy and can be reopened locally only before any possible native handoff.</summary>
    [Fact]
    public async Task OpensAndReopensLazyBindingWithoutHttpOrCredentials()
    {
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new((request, token) => throw new InvalidOperationException("Lazy open has no native operation."));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, credentials, handler);
        ProviderBinding binding = await provider.OpenAsync(Context(), CancellationToken.None);
        Assert.Equal(Lazy(), binding);
        Assert.Equal(binding, await provider.ReopenAsync(Context(), binding, CancellationToken.None));
        Assert.Equal(0, credentials.Calls);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>Reopen reads the confirmed head and preserves its distinct first native identity.</summary>
    [Fact]
    public async Task ReopensConfirmedHeadWithoutReplacingFirstIdentity()
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://generativelanguage.googleapis.com/v1/interactions/interaction_head", request.RequestUri?.AbsoluteUri);
            Assert.Equal("fixture-key-1", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(ScriptedHttpHandler.Json(Json("head_completed")));
        });
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        Assert.Equal(Binding(), await provider.ReopenAsync(Context(), Binding(), CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Known native identity without a confirmed head is not permission to start another root interaction.</summary>
    [Fact]
    public async Task RefusesUnconfirmedBoundHeadWithoutHttp()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("head_completed"))));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAsync(Context(), Binding() with { ContinuationState = null }, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderSessionExpired, failure.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>A pending native resource cannot verify the persisted completed continuation head.</summary>
    [Fact]
    public async Task RefusesPendingHeadDuringReopen()
    {
        JsonNode body = JsonNode.Parse(Json("head_completed"))!;
        body["status"] = "in_progress";
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body.ToJsonString())));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAsync(Context(), Binding(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderUnavailable, failure.Failure.Code);
        Assert.Equal(1, handler.Calls);
    }
}
