using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Gemini's model resource prefix belongs to its protocol and does not rewrite frozen model selection.</summary>
public sealed class GeminiModelResourceTests
{
    private static ProviderSessionContext Context(string model)
        => new(Guid.NewGuid(), model, ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());

    /// <summary>Both bare aliases and canonical resource names address the same metadata resource.</summary>
    [Theory]
    [InlineData("alias")]
    [InlineData("models/alias")]
    public async Task ReadsCanonicalResourceWithoutDuplicatingPrefix(string model)
    {
        string? observedPath = null;
        using ScriptedHttpHandler handler = new((request, _) =>
        {
            observedPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(ScriptedHttpHandler.Json("""{"name":"models/canonical-alias-target"}"""));
        });
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderSessionContext context = Context(model);
        await provider.ValidateAvailabilityAsync(context, CancellationToken.None);
        Assert.Equal("/v1/models/alias", observedPath);
        Assert.Equal(model, context.ModelId);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Missing or dot-only resource identity cannot become a request outside the models resource.</summary>
    [Theory]
    [InlineData("models/")]
    [InlineData("models/.")]
    [InlineData("models/..")]
    public async Task RejectsInvalidCanonicalResourceWithoutHttp(string model)
    {
        using ScriptedHttpHandler handler = new((_, _) => Task.FromResult(ScriptedHttpHandler.Json("{}")));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => provider.ValidateAvailabilityAsync(Context(model), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, handler.Calls);
    }
}
