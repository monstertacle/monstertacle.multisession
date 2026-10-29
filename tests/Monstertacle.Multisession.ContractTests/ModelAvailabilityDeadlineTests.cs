using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Monstertacle.Multisession.Providers.OpenAI;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Metadata reads use bounded existing HTTP and total deadlines without generating replacement requests.</summary>
public sealed class ModelAvailabilityDeadlineTests
{
    /// <summary>An uncooperative HTTP read reports a finite local timeout while caller cancellation remains cancellation.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BoundsUncooperativeMetadataRead(bool gemini, bool callerCancels)
    {
        using HttpTestClock clock = new();
        using CancellationTokenSource caller = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<HttpResponseMessage> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((_, _) => { entered.TrySetResult(); return finish.Task; });
        using IDisposable native = gemini
            ? new GeminiSessionProvider(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler, clock)
            : new OpenAiSessionProvider(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler, clock);
        ISessionAvailabilityProvider provider = Assert.IsAssignableFrom<ISessionAvailabilityProvider>(native);
        ProviderSessionContext context = new(Guid.NewGuid(), "selected", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
        Task checking = provider.ValidateAvailabilityAsync(context, caller.Token).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForTimerAsync(RuntimeDefaults.RequestTimeout).WaitAsync(TimeSpan.FromSeconds(5));
            if (callerCancels)
            {
                caller.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checking.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            else
            {
                clock.Fire(RuntimeDefaults.RequestTimeout);
                SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => checking.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(ErrorCode.TimedOut, failure.Failure.Code);
            }
            Assert.Equal(1, handler.Calls);
        }
        finally { finish.TrySetResult(ScriptedHttpHandler.Json("{}")); }
    }
}
