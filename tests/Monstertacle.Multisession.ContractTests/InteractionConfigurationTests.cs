using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Interactions;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Validates approved native endpoints and rejects unsupported local context before I/O.</summary>
public sealed class InteractionConfigurationTests
{
    /// <summary>Rejects ambiguous, credential-bearing, remote plaintext or incomplete connection configuration.</summary>
    [Theory]
    [InlineData("http://remote.example/v1/", false, "reference")]
    [InlineData("http://localhost/v1/", false, "reference")]
    [InlineData("https://user:private@compatible.example/v1/", false, "reference")]
    [InlineData("https://compatible.example/v1/?query=private", false, "reference")]
    [InlineData("https://compatible.example/v1/#fragment", false, "reference")]
    [InlineData("relative/v1", false, "reference")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/", false, "reference")]
    [InlineData("https://compatible.example/v1/", false, " ")]
    public void RejectsInvalidConnection(string endpoint, bool localHttp, string reference)
    {
        InteractionOptions options = new() { Endpoint = new(endpoint, UriKind.RelativeOrAbsolute), CredentialReference = reference, AllowLocalHttp = localHttp };
        SessionOperationException error = Assert.Throws<SessionOperationException>(() => new InteractionSessionProvider(options, new FixtureCredentials()));
        Assert.Equal(ErrorCode.InvalidConfiguration, error.Failure.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Preserves a custom base path with either trailing-slash spelling and explicit local HTTP opt-in.</summary>
    [Theory]
    [InlineData("https://compatible.example/prefix/v1")]
    [InlineData("https://compatible.example/prefix/v1/")]
    [InlineData("http://127.0.0.1/prefix/v1")]
    public async Task KeepsConfiguredBasePath(string endpoint)
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal("/prefix/v1/interactions/head", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json("""{"id":"head","object":"interaction","status":"completed","steps":[]}"""));
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new(endpoint), CredentialReference = "reference", AllowLocalHttp = true }, new FixtureCredentials(), handler);
        await provider.ReopenAsync(Context(), new("gemini-interactions", 1, "root", "head"), CancellationToken.None);
    }

    /// <summary>Rejects incompatible or absent persisted identity without any native request.</summary>
    [Theory]
    [InlineData("other-adapter", 1, "native-root")]
    [InlineData("gemini-interactions", 2, "native-root")]
    [InlineData("gemini-interactions", 1, null)]
    public async Task RejectsIncompatibleBinding(string adapterId, int version, string? reference)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("""{"id":"head","object":"interaction","status":"completed","steps":[]}""")));
        FixtureCredentials credentials = new();
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, credentials, handler);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ReopenAsync(Context(), new(adapterId, version, reference, reference is null ? "head" : null), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, error.Failure.Code);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, credentials.Calls);
    }

    /// <summary>Never pretends a model response is an agent task or silently drops unknown settings.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsUnsupportedContextWithoutOpening(bool agentTask)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json("""{"id":"head","object":"interaction","status":"completed","steps":[]}""")));
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderSessionContext context = agentTask ? Context() with { ExecutionKind = ExecutionKind.AgentTask }
            : new(Guid.NewGuid(), "opaque", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string> { ["unknown-setting"] = "value" });
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.OpenAsync(context, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedProviderAction, error.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }

    private static ProviderSessionContext Context() => new(Guid.NewGuid(), "opaque", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
}
