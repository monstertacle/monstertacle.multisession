using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Runs one framework request on actual SQLite and reads it back through a reopened runtime.
internal static class FrameworkEvidenceScenario
{
    private static readonly AgentProfile[] _profiles = [new("agent", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()))];

    private static ProviderRegistration[] Providers(FrameworkTestAgent agent)
        => [new("framework", "evidence-v1", 1, new FrameworkSessionProvider(new() { AdapterId = "evidence-v1", QualifiedOutcomes = true }, _ => agent))];

    internal static async Task<(Guid Session, Guid Request, RequestSnapshot Ended)> RunOnceAsync(PrivateStoreDirectory directory, FrameworkTestAgent agent)
    {
        await using RetainedTestLease lease = new(directory);
        ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default);
        await using SessionRuntime runtime = new(store, lease, Providers(agent), _profiles);
        await runtime.StartAsync(directory.Options, default);
        Guid session = (await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "agent", new(null, null), default)).SessionId;
        Guid request = (await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session, "effect", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        while (true)
        {
            RequestSnapshot current = (await runtime.Requests.GetRequestAsync(request, deadline.Token))!;
            if (current.State is not (RequestState.Queued or RequestState.Running)) { return (session, request, current); }
            await Task.Delay(10, deadline.Token);
        }
    }

    internal static async Task<(RequestSnapshot? Request, SessionSnapshot? Session)> ReopenAsync(PrivateStoreDirectory directory, FrameworkTestAgent agent, Guid session, Guid request)
    {
        await using RetainedTestLease lease = new(directory);
        ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, directory.TrustScopeId, directory.Options, default);
        await using SessionRuntime runtime = new(store, lease, Providers(agent), _profiles);
        await runtime.StartAsync(directory.Options, default);
        return (await runtime.Requests.GetRequestAsync(request, default), await runtime.Sessions.GetSessionAsync(session, default));
    }
}
