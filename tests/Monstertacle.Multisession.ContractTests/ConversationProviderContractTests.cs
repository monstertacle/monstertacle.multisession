using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Native OpenAI contract against synthetic local HTTP responses, never live credentials.</summary>
public sealed class ConversationProviderContractTests
{
    private static ProviderSessionContext Context() => new(Guid.NewGuid(), "opaque-deployment", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
    private static ConversationOptions Options() => new() { Endpoint = new("https://compatible.example/v1/"), CredentialReference = "host-reference" };

    /// <summary>Creates one native Conversation and persists its identity separately from Response IDs.</summary>
    [Fact]
    public async Task OpensConversationWithFreshHostCredential()
    {
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://compatible.example/v1/conversations", request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("fixture-key-1", request.Headers.Authorization?.Parameter);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Empty(body.RootElement.EnumerateObject());
            return ScriptedHttpHandler.Json("""{"id":"conv_fixture","object":"conversation","created_at":1} """);
        });
        using ConversationSessionProvider provider = new(Options(), credentials, handler);
        ProviderBinding binding = await provider.OpenAsync(Context(), CancellationToken.None);
        Assert.Equal("openai-responses", binding.AdapterId);
        Assert.Equal(1, binding.FormatVersion);
        Assert.Equal("conv_fixture", binding.SessionReference);
        Assert.Null(binding.ContinuationState);
        Assert.Equal(1, credentials.Calls);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Reopens the same native Conversation with a new credential resolution and no POST.</summary>
    [Fact]
    public async Task ReopensSameConversationWithoutCreatingReplacement()
    {
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/conversations/conv_fixture", request.RequestUri?.AbsolutePath);
            Assert.Equal($"fixture-key-{credentials.Calls}", request.Headers.Authorization?.Parameter);
            return Task.FromResult(ScriptedHttpHandler.Json("""{"id":"conv_fixture","object":"conversation","created_at":1} """));
        });
        using ConversationSessionProvider provider = new(Options(), credentials, handler);
        ProviderBinding original = new("openai-responses", 1, "conv_fixture", null);
        Assert.Equal(original, await provider.ReopenAsync(Context(), original, CancellationToken.None));
        Assert.Equal(original, await provider.ReopenAsync(Context(), original, CancellationToken.None));
        Assert.Equal(2, credentials.Calls);
        Assert.Equal(2, handler.Calls);
    }
}
