using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Interactions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Native continuation failure isolates one session while preserving independent runtime progress.</summary>
public sealed class ContinuationRuntimeTests
{
    /// <summary>A known-ended first failure blocks its queued follower without inventing uncertain external execution.</summary>
    [Fact]
    public async Task BlocksUnusableNativeSessionWhileOtherSessionCompletes()
    {
        using UnavailableContinuationWire wire = new();
        CompletionBarrier one = new(1);
        CompletionBarrier both = new(2);
        InMemorySessionStore store = new() { DispatchState = new() { Completed = () => { one.Record(); both.Record(); } } };
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), wire.Handler);
        await using SessionRuntime runtime = new(store, new TestStateDirectoryLease(Path.GetTempPath()),
            [new("native", "gemini-interactions", 1, provider)],
            [new("profile", "native", "opaque-model", ExecutionKind.ModelResponse, new("revision", new Dictionary<string, string>()))]);
        await runtime.StartAsync(new() { StateDirectory = Path.GetTempPath(), Retention = new(true, null), MaxConcurrentRequests = 2 }, CancellationToken.None);
        try
        {
            SessionSnapshot first = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
            SessionSnapshot independent = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
            RequestReceipt a = await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "first", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            await wire.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            RequestReceipt follower = await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "follower", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            RequestReceipt b = await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(independent.SessionId, "independent", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            await one.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(RequestState.Succeeded, (await runtime.Requests.GetRequestAsync(b.RequestId, CancellationToken.None))?.State);
            wire.Release();
            await both.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            RequestSnapshot? ended = await runtime.Requests.GetRequestAsync(a.RequestId, CancellationToken.None);
            Assert.Equal(RequestState.Failed, ended?.State);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, ended?.Certainty);
            Assert.Equal(SessionBlockReason.ProviderUnavailable, (await runtime.Sessions.GetSessionAsync(first.SessionId, CancellationToken.None))?.BlockReason);
            Assert.Equal(RequestState.Queued, (await runtime.Requests.GetRequestAsync(follower.RequestId, CancellationToken.None))?.State);
            Assert.Empty(store.Reservations);
            Assert.Empty((await runtime.Recovery.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items);
            Assert.Equal(2, wire.Calls);
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Requests.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "new", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.SessionBlocked, failure.Failure.Code);
            await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
        finally { wire.Release(); }
    }
}
