using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Canonical caller identities and persistent host dispatch authority.</summary>
public sealed class HostAuthorityTests
{
    private static async Task<SessionRuntime> CreateAsync(PrivateStoreDirectory directory, RuntimeOptions options, FrameworkTestAgent agent)
    {
        RetainedTestLease lease = new(directory);
        ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, options, default);
        FrameworkSessionProvider provider = new(new() { AdapterId = "host-authority" }, _ => agent);
        SessionRuntime runtime = new(store, lease, [new("framework", "host-authority", 1, provider)],
            [new("agent", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()))]);
        await runtime.StartAsync(options, default);
        return runtime;
    }

    /// <summary>The caller can recover the exact identity after losing a returned receipt.</summary>
    [Fact]
    public async Task UsesCallerIdsAndRejectsDuplicateBeforeAnotherOpen()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new();
        await using SessionRuntime runtime = await CreateAsync(directory, directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled }, agent);
        Guid sessionId = Guid.NewGuid();
        SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(sessionId, "agent", new(null, null), default);
        Assert.Equal(sessionId, session.SessionId);
        Guid requestId = Guid.NewGuid();
        RequestReceipt receipt = await runtime.Requests.SubmitAsync(requestId, new(sessionId, "new input", TimeSpan.FromSeconds(5), null, false), default);
        Assert.Equal(requestId, receipt.RequestId);
        Assert.Equal(sessionId, (await runtime.Requests.GetRequestAsync(requestId, default))?.SessionId);
        SessionOperationException duplicate = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Sessions.CreateSessionAsync(sessionId, "agent", new(null, null), default).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, duplicate.Failure.Code);
        Assert.Equal(1, agent.Creates);
    }

    /// <summary>Controlled startup does not run admitted work and stop preserves its queue state.</summary>
    [Fact]
    public async Task ControlledRuntimeNeverDispatchesOrCancelsQueuedWorkAtStop()
    {
        await using PrivateStoreDirectory directory = new();
        RuntimeOptions options = directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled };
        FrameworkTestAgent agent = new();
        Guid request;
        await using (SessionRuntime runtime = await CreateAsync(directory, options, agent))
        {
            SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default);
            request = (await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "held", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.Empty(agent.Inputs);
            await runtime.StopAsync(TimeSpan.FromSeconds(5), default);
        }
        await using RetainedTestLease lease = new(directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, directory.TrustScopeId, options, default);
        await store.StartAsync(lease, default);
        Assert.Equal(RequestState.Queued, (await store.GetRequestAsync(request, default))?.State);
    }

    /// <summary>Only the explicitly named eligible FIFO head can execute.</summary>
    [Fact]
    public async Task ExplicitDispatchCannotSubstituteAnotherQueuedHead()
    {
        await using PrivateStoreDirectory directory = new();
        RuntimeOptions options = directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled };
        FrameworkTestAgent agent = new();
        await using SessionRuntime runtime = await CreateAsync(directory, options, agent);
        SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default);
        Guid first = (await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "first", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
        Guid second = (await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "second", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
        Assert.False(await runtime.DispatchAsync(second, default));
        Assert.Empty(agent.Inputs);
        Assert.True(await runtime.DispatchAsync(first, default));
        Assert.Equal("first", Assert.Single(agent.Inputs));
        Assert.Equal(RequestState.Queued, (await runtime.Requests.GetRequestAsync(second, default))?.State);
    }

    /// <summary>A later reader cannot change dispatch ownership by changing runtime options.</summary>
    [Fact]
    public async Task StoreRejectsDispatchModeChangeBeforeStartup()
    {
        await using PrivateStoreDirectory directory = new();
        RuntimeOptions options = directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled };
        await using (RetainedTestLease lease = new(directory))
        await using (ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, options, default)) { }
        await using RetainedTestLease changed = new(directory);
        await Assert.ThrowsAsync<SessionOperationException>(() => SqliteStoreFactory.OpenExistingAsync(changed, directory.TrustScopeId, directory.Options, default).AsTask());
    }
    /// <summary>Admission uses the previously persisted caller identity without an internal replacement.</summary>
    [Fact]
    public async Task RequestIdentityIsExactAndDuplicateAdmissionDoesNotCreateAnotherRow()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new();
        await using SessionRuntime runtime = await CreateAsync(directory, directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled }, agent);
        SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default);
        Guid requestId = Guid.NewGuid();
        SubmitRequest input = new(session.SessionId, "new input", TimeSpan.FromSeconds(5), null, false);
        RequestReceipt receipt = await runtime.Requests.SubmitAsync(requestId, input, default);
        Assert.Equal(requestId, receipt.RequestId);
        await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Requests.SubmitAsync(requestId, input, default).AsTask());
        Assert.Single((await runtime.Requests.ListRequestsAsync(session.SessionId, 10, null, default)).Items);
    }

    /// <summary>Empty host identities are refused before any external session is created.</summary>
    [Fact]
    public async Task EmptySessionIdentityDoesNotOpenAnAgent()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new();
        await using SessionRuntime runtime = await CreateAsync(directory, directory.Options with { DispatchMode = RuntimeDispatchMode.HostControlled }, agent);
        await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Sessions.CreateSessionAsync(Guid.Empty, "agent", new(null, null), default).AsTask());
        Assert.Equal(0, agent.Creates);
    }
}
