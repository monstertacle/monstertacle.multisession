using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.GeminiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Direct native consumers cannot start an unlimited execution or use invalid text limits.</summary>
public sealed class GeminiCallValidationTests
{
    /// <summary>Rejects invalid finite-call constraints before any native POST.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsInvalidCallLimitsBeforeHttp(bool timeout)
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("new_completed"))));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderCall call = timeout ? Call() with { Timeout = Timeout.InfiniteTimeSpan } : Call(outputBytes: 0);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => provider.SendAsync(call, Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, error.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, error.Failure.Certainty);
        Assert.Equal(0, handler.Calls);
    }
}
