using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Actual SQLite/runtime composition, restart and uncertainty using a real framework agent.</summary>
public sealed class FrameworkRuntimeTests
{
    private static FrameworkSessionProvider Provider(FrameworkTestAgent agent) => new(new() { AdapterId = "framework-v1" }, _ => agent);
    private static ProviderRegistration Registration(FrameworkSessionProvider provider) => new("framework", "framework-v1", 1, provider);
    private static AgentProfile Profile() => new("agent", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()));
    private static async Task<RequestSnapshot> EndAsync(SessionRuntime runtime, Guid request)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            RequestSnapshot current = (await runtime.Requests.GetRequestAsync(request, deadline.Token))!;
            if (current.State is not (RequestState.Queued or RequestState.Running)) { return current; }
            await Task.Delay(10, deadline.Token);
        }
    }
    /// <summary>Reopening the actual store restores framework continuation and sends only the next input.</summary>
    [Fact]
    public async Task ContinuesConfirmedSessionAfterRuntimeRecreation()
    {
        await using PrivateStoreDirectory directory = new();
        Guid sessionId;
        FrameworkTestAgent firstAgent = new();
        await using (RetainedTestLease lease = new(directory))
        {
            ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default);
            await using SessionRuntime runtime = new(store, lease, [Registration(Provider(firstAgent))], [Profile()]);
            await runtime.StartAsync(directory.Options, default);
            sessionId = (await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default)).SessionId;
            RequestReceipt request = await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(sessionId, "first", TimeSpan.FromSeconds(5), null, false), default);
            Assert.Equal("turn-1", (await EndAsync(runtime, request.RequestId)).Result?.Output);
        }
        FrameworkTestAgent secondAgent = new();
        await using RetainedTestLease reopenedLease = new(directory);
        ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(reopenedLease, directory.TrustScopeId, directory.Options, default);
        await using SessionRuntime restarted = new(reopened, reopenedLease, [Registration(Provider(secondAgent))], [Profile()]);
        await restarted.StartAsync(directory.Options, default);
        RequestReceipt follow = await restarted.Requests.SubmitAsync(Guid.NewGuid(), new(sessionId, "second", TimeSpan.FromSeconds(5), null, false), default);
        Assert.Equal("turn-2", (await EndAsync(restarted, follow.RequestId)).Result?.Output);
        Assert.Equal("second", Assert.Single(secondAgent.Inputs));
        Assert.Equal(0, secondAgent.Creates);
    }

    /// <summary>Uncertain invocation retains reservations through restart and cannot be repeated.</summary>
    [Fact]
    public async Task UncertainAgentFailureRemainsBlockedAfterRestart()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new() { Running = _ => throw new IOException("private") };
        Guid sessionId;
        Guid requestId;
        await using (RetainedTestLease lease = new(directory))
        {
            ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default);
            await using SessionRuntime runtime = new(store, lease, [Registration(Provider(agent))], [Profile()]);
            await runtime.StartAsync(directory.Options, default);
            sessionId = (await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default)).SessionId;
            requestId = (await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(sessionId, "effect", TimeSpan.FromSeconds(5), "workspace", true), default)).RequestId;
            Assert.Equal(ExecutionCertainty.Unknown, (await EndAsync(runtime, requestId)).Certainty);
        }
        await using RetainedTestLease reopenedLease = new(directory);
        ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(reopenedLease, directory.TrustScopeId, directory.Options, default);
        await using SessionRuntime restarted = new(reopened, reopenedLease, [Registration(Provider(agent))], [Profile()]);
        await restarted.StartAsync(directory.Options, default);
        Assert.Equal(ExecutionCertainty.Unknown, (await restarted.Requests.GetRequestAsync(requestId, default))?.Certainty);
        await Assert.ThrowsAsync<SessionOperationException>(() => restarted.Requests.SubmitAsync(Guid.NewGuid(), new(sessionId, "replacement", TimeSpan.FromSeconds(5), "workspace", true), default).AsTask());
        Assert.Single(agent.Inputs);
        Assert.Equal(1, (await reopened.GetRuntimeStatisticsAsync(default)).OccupiedReservations);
    }
}
