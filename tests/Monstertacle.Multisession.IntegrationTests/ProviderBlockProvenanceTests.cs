using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>
/// A persisted provider block records state, not its cause. A lost continuation and the public store setter leave the same persisted
/// shape. Only a block the configuration check itself set (ConfigurationUnavailable) is eligible for clearing by a later successful
/// configuration check.
/// </summary>
public sealed class ProviderBlockProvenanceTests
{
    /// <summary>The completion pathway and the public setter persist identical blocks; revalidation clears only the configuration block.</summary>
    [Fact]
    public async Task PersistedProviderBlockDoesNotRecordItsCause()
    {
        await using PrivateStoreDirectory directory = new();
        FrameworkTestAgent agent = new() { FailSerialization = true };
        FrameworkSessionProvider provider = new(new() { AdapterId = "block-provenance" }, _ => agent);
        ProviderRegistration[] providers = [new("framework", "block-provenance", 1, provider)];
        AgentProfile[] profiles = [new("profile", "framework", "host-model", ExecutionKind.AgentTask, new("v1", new Dictionary<string, string>()))];
        Guid lost, marked, configured;
        await using (RetainedTestLease lease = new(directory))
        await using (ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, directory.Options, default))
        {
            await store.StartAsync(lease, default);
            SessionCatalog sessions = new(store, providers, profiles);
            lost = (await sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), default)).SessionId;
            marked = (await sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), default)).SessionId;
            configured = (await sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), default)).SessionId;

            // Normal completion pathway: a confirmed end whose continuation is unavailable.
            await new RequestAdmission(store, directory.Options).SubmitAsync(Guid.NewGuid(), new(lost, "effect", TimeSpan.FromSeconds(5), null, false), default);
            Assert.True(await new RequestDispatcher(store, providers, directory.Options).TryDispatchAsync(default));
            // Public store setter: no continuation was involved at all.
            await store.MarkSessionBlockedAsync(marked, SessionBlockReason.ProviderUnavailable, default);
            // Normal configuration pathway: the session's own configuration check fails.
            await store.CommitConfigurationAvailabilityAsync(configured, false, default, ErrorCode.ProviderUnavailable);

            Assert.Equal(Shape(SessionBlockReason.ProviderUnavailable, false, null), Shape(await store.GetSessionAsync(lost, default)));
            Assert.Equal(Shape(await store.GetSessionAsync(lost, default)), Shape(await store.GetSessionAsync(marked, default)));
            Assert.Equal(Shape(SessionBlockReason.ProviderUnavailable, true, ErrorCode.ProviderUnavailable), Shape(await store.GetSessionAsync(configured, default)));

            // A later successful configuration check is eligible to clear only its own block.
            foreach (Guid session in new[] { lost, marked, configured }) { await store.CommitConfigurationAvailabilityAsync(session, true, default); }
        }
        await using RetainedTestLease reopenedLease = new(directory);
        await using ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(reopenedLease, directory.TrustScopeId, directory.Options, default);
        await reopened.StartAsync(reopenedLease, default);
        Assert.Equal(Shape(SessionBlockReason.ProviderUnavailable, false, null), Shape(await reopened.GetSessionAsync(lost, default)));
        Assert.Equal(Shape(SessionBlockReason.ProviderUnavailable, false, null), Shape(await reopened.GetSessionAsync(marked, default)));
        Assert.Equal(Shape(SessionBlockReason.None, false, null), Shape(await reopened.GetSessionAsync(configured, default)));
    }

    private static (SessionBlockReason, bool, ErrorCode?) Shape(SessionBlockReason reason, bool configurationUnavailable, ErrorCode? error)
        => (reason, configurationUnavailable, error);

    private static (SessionBlockReason, bool, ErrorCode?) Shape(SessionSnapshot? session)
    {
        Assert.NotNull(session);
        return (session.BlockReason, session.ConfigurationUnavailable, session.AvailabilityError);
    }
}
