using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Cancellation before new input preserves a confirmed usable continuation.</summary>
public sealed class FrameworkPreInvocationTests
{
    /// <summary>A later turn succeeds on the same session after cancellation during restoration.</summary>
    [Fact]
    public async Task CancelledRestorationDoesNotPoisonConfirmedSession()
    {
        await using PrivateStoreDirectory directory = new();
        await using RetainedTestLease lease = new(directory);
        await using ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default);
        await store.StartAsync(lease, default);
        using CancellationTokenSource cancellation = new();
        FrameworkTestAgent agent = new()
        {
            BeforeRestore = token => { cancellation.Cancel(); return ValueTask.FromCanceled(token); },
        };
        FrameworkSessionProvider provider = new(new() { AdapterId = "pre-invocation" }, _ => agent);
        ProviderRegistration[] providers = [new("framework", "pre-invocation", 1, provider)];
        AgentProfile[] profiles = [new("profile", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()))];
        SessionCatalog sessions = new(store, providers, profiles);
        Guid sessionId = (await sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), default)).SessionId;
        RequestAdmission admission = new(store, directory.Options);
        RequestDispatcher dispatcher = new(store, providers, directory.Options);
        Guid cancelled = (await admission.SubmitAsync(Guid.NewGuid(), new(sessionId, "not sent", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
        Assert.True(await dispatcher.TryDispatchAsync(cancellation.Token));
        Assert.Equal(ExecutionCertainty.NotStarted, (await store.GetRequestAsync(cancelled, default))?.Certainty);
        Assert.Equal(SessionBlockReason.None, (await store.GetSessionAsync(sessionId, default))?.BlockReason);
        Assert.Empty(agent.Inputs);
        agent.BeforeRestore = null;
        Guid follow = (await admission.SubmitAsync(Guid.NewGuid(), new(sessionId, "follow", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
        Assert.True(await dispatcher.TryDispatchAsync(default));
        Assert.Equal(RequestState.Succeeded, (await store.GetRequestAsync(follow, default))?.State);
        Assert.Equal("follow", Assert.Single(agent.Inputs));
    }
}
