using System.Net;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Preserves native identity and exposes sanitized failures without POST retries.</summary>
public sealed class ConversationOpeningFailureTests
{
    private static ProviderSessionContext Context() => new(Guid.NewGuid(), "opaque", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());

    /// <summary>Maps HTTP failure categories without including arbitrary private server details.</summary>
    [Theory]
    [InlineData(401, ErrorCode.AuthenticationFailed)]
    [InlineData(403, ErrorCode.AuthenticationFailed)]
    [InlineData(429, ErrorCode.ProviderRateLimited)]
    [InlineData(503, ErrorCode.ProviderUnavailable)]
    [InlineData(302, ErrorCode.ProviderUnavailable)]
    public async Task SanitizesHttpFailureAndNeverRetriesPost(int status, ErrorCode expected)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("""{"error":"private server details"}""", (HttpStatusCode)status)));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.OpenAsync(Context(), CancellationToken.None).AsTask());
        Assert.Equal(expected, error.Failure.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>A missing or different native identity is never silently replaced by a new Conversation.</summary>
    [Theory]
    [InlineData(404, "{}")]
    [InlineData(200, "{\"id\":\"conv_other\",\"object\":\"conversation\"}")]
    public async Task RejectsExpiredOrReplacedConversation(int status, string body)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body, (HttpStatusCode)status)));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderBinding binding = new("openai-responses", 1, "conv_fixture", null);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAsync(Context(), binding, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderSessionExpired, error.Failure.Code);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("conv_fixture", binding.SessionReference);
    }

    /// <summary>Malformed responses cannot create an unbound or non-Conversation session.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":null,\"object\":\"conversation\"}")]
    [InlineData("{\"id\":\"conv_fixture\",\"object\":\"response\"}")]
    [InlineData("private malformed body")]
    public async Task RejectsMalformedNativeOpening(string body)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(body)));
        using ConversationSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.OpenAsync(Context(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderUnavailable, error.Failure.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private", error.ToString(), StringComparison.Ordinal);
    }
}
