using System.Net;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Conversations;
using Monstertacle.Multisession.Providers.Interactions;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Model availability uses metadata reads with current credentials and never a generated probe request.</summary>
public sealed class NativeModelAvailabilityTests
{
    private static IDisposable Provider(bool gemini, ScriptedHttpHandler handler, FixtureCredentials credentials)
        => gemini ? new InteractionSessionProvider(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, credentials, handler)
            : new ConversationSessionProvider(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, credentials, handler);
    private static ProviderSessionContext Context(string model)
        => new(Guid.NewGuid(), model, ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());

    /// <summary>Alias metadata does not change the frozen selection; each read resolves a fresh credential.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsSelectedModelWithFreshCredentialWithoutGeneration(bool gemini)
    {
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/v1/models/opaque%2Falias%20value", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            Assert.Null(request.Content);
            string credential = gemini ? Assert.Single(request.Headers.GetValues("x-goog-api-key")) : request.Headers.Authorization!.Parameter!;
            Assert.Equal($"fixture-key-{credentials.Calls}", credential);
            return Task.FromResult(ScriptedHttpHandler.Json(gemini ? """{"name":"models/canonical-alias-target"}""" : """{"id":"canonical-alias-target","object":"model"}"""));
        });
        using IDisposable native = Provider(gemini, handler, credentials);
        ISessionAvailabilityProvider provider = Assert.IsAssignableFrom<ISessionAvailabilityProvider>(native);
        ProviderSessionContext context = Context("opaque/alias value");
        await provider.ValidateAvailabilityAsync(context, CancellationToken.None);
        await provider.ValidateAvailabilityAsync(context, CancellationToken.None);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(2, credentials.Calls);
        Assert.Equal("opaque/alias value", context.ModelId);
    }

    /// <summary>Missing model and rejected credentials are distinct from expired native session identity.</summary>
    [Theory]
    [InlineData(false, 401)]
    [InlineData(true, 401)]
    [InlineData(false, 403)]
    [InlineData(true, 403)]
    [InlineData(false, 404)]
    [InlineData(true, 404)]
    [InlineData(false, 200)]
    [InlineData(true, 200)]
    public async Task RejectsUnavailableOrMalformedModelMetadata(bool gemini, int status)
    {
        using ScriptedHttpHandler handler = new((_, _) => Task.FromResult(ScriptedHttpHandler.Json("""{"error":{"message":"private metadata payload"}}""", (HttpStatusCode)status)));
        using IDisposable native = Provider(gemini, handler, new());
        ISessionAvailabilityProvider provider = Assert.IsAssignableFrom<ISessionAvailabilityProvider>(native);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ValidateAvailabilityAsync(Context("selected-model"), CancellationToken.None).AsTask());
        Assert.Equal(status is 401 or 403 ? ErrorCode.AuthenticationFailed : ErrorCode.ProviderUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private metadata payload", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Invalid model path segments cannot escape the models resource or initiate HTTP.</summary>
    [Theory]
    [InlineData(false, ".")]
    [InlineData(true, ".")]
    [InlineData(false, "..")]
    [InlineData(true, "..")]
    [InlineData(false, "")]
    [InlineData(true, "")]
    public async Task RejectsInvalidSelectionWithoutHttp(bool gemini, string model)
    {
        using ScriptedHttpHandler handler = new((_, _) => Task.FromResult(ScriptedHttpHandler.Json("{}")));
        using IDisposable native = Provider(gemini, handler, new());
        ISessionAvailabilityProvider provider = Assert.IsAssignableFrom<ISessionAvailabilityProvider>(native);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ValidateAvailabilityAsync(Context(model), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }
}
