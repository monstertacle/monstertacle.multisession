using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Known termination survives cancellation concurrent with serialization failure.</summary>
public sealed class FrameworkTerminalRaceTests
{
    /// <summary>The unchanged binding blocks continuation while releasing confirmed-ended capacity.</summary>
    [Fact]
    public async Task TerminalSerializationFailurePreservesFinalityAcrossStoreReopen()
    {
        await using PrivateStoreDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        FrameworkTestAgent agent = new() { FailSerialization = true, AfterTurn = cancellation.Cancel };
        FrameworkSessionProvider provider = new(new() { AdapterId = "terminal-race" }, _ => agent);
        ProviderRegistration[] providers = [new("framework", "terminal-race", 1, provider)];
        AgentProfile[] profiles = [new("profile", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()))];
        Guid requestId;
        Guid sessionId;
        await using (RetainedTestLease lease = new(directory))
        await using (ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default))
        {
            await store.StartAsync(lease, default);
            SessionCatalog sessions = new(store, providers, profiles);
            sessionId = (await sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), default)).SessionId;
            RequestAdmission admission = new(store, directory.Options);
            requestId = (await admission.SubmitAsync(Guid.NewGuid(), new(sessionId, "effect", TimeSpan.FromSeconds(5), null, false), default)).RequestId;
            RequestDispatcher dispatcher = new(store, providers, directory.Options);
            Assert.True(await dispatcher.TryDispatchAsync(cancellation.Token));
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, (await store.GetRequestAsync(requestId, default))?.Certainty);
            Assert.Equal(SessionBlockReason.ProviderUnavailable, (await store.GetSessionAsync(sessionId, default))?.BlockReason);
            Assert.Equal(0, (await store.GetRuntimeStatisticsAsync(default)).OccupiedReservations);
        }
        await using RetainedTestLease reopenedLease = new(directory);
        await using ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(reopenedLease, directory.TrustScopeId, directory.Options, default);
        await reopened.StartAsync(reopenedLease, default);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, (await reopened.GetRequestAsync(requestId, default))?.Certainty);
        Assert.Equal(SessionBlockReason.ProviderUnavailable, (await reopened.GetSessionAsync(sessionId, default))?.BlockReason);
        Assert.Single(agent.Inputs);
    }
}
