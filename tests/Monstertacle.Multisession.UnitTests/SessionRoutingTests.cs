using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks local identity, host profile selection and storage-before-open ordering.</summary>
public sealed class SessionRoutingTests
{
    private static SessionCatalog CreateCatalog(InMemorySessionStore store)
    {
        ProviderRegistration[] providers = [new("connection", "controlled", 1, new ControlledProvider())];
        AgentProfile[] profiles = [new("profile", "connection", "opaque-model", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()))];
        return new SessionCatalog(store, providers, profiles);
    }

    /// <summary>Creates an isolated local identity and binds only the explicitly selected adapter.</summary>
    [Fact]
    public async Task PersistsIdentityBeforeOpeningSelectedAdapter()
    {
        InMemorySessionStore store = new();
        int selectedOpens = 0;
        int otherOpens = 0;
        ControlledProvider selected = new()
        {
            Opening = context =>
            {
                selectedOpens++;
                Assert.NotEqual(Guid.Empty, context.SessionId);
                Assert.NotNull(store.FindSession(context.SessionId));
                Assert.Equal("opaque/provider/model", context.ModelId);
            },
        };
        ControlledProvider other = new() { Opening = _ => otherOpens++ };
        ProviderRegistration[] providers = [new("connection-a", "controlled", 1, selected), new("connection-b", "controlled", 1, other)];
        AgentProfile[] profiles = [new("profile-a", "connection-a", "opaque/provider/model", ExecutionKind.AgentTask, new("revision-1", new Dictionary<string, string>()))];
        SessionCatalog catalog = new(store, providers, profiles);
        SessionExecutionContext workspace = new(Path.GetTempPath(), "canonical-workspace");

        SessionSnapshot? created = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile-a", workspace, CancellationToken.None);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.SessionId);
        Assert.Equal("connection-a", created.ProviderId);
        Assert.Equal("profile-a", created.AgentProfileId);
        Assert.Equal(ExecutionKind.AgentTask, created.Context.ExecutionKind);
        Assert.Equal(workspace, created.Context.ExecutionContext);
        Assert.Equal("controlled", created.Binding?.AdapterId);
        Assert.Equal(1, selectedOpens);
        Assert.Equal(0, otherOpens);
        Assert.Equal(created, store.FindSession(created.SessionId));
    }

    /// <summary>Reads the stored snapshot while refusing to search another instance's store.</summary>
    [Fact]
    public async Task LooksUpOnlySessionsInItsOwnStore()
    {
        SessionCatalog first = CreateCatalog(new InMemorySessionStore());
        SessionCatalog other = CreateCatalog(new InMemorySessionStore());
        SessionSnapshot created = await first.CreateSessionAsync(Guid.NewGuid(), "profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None);

        SessionSnapshot? found = await first.GetSessionAsync(created.SessionId, CancellationToken.None);

        Assert.Equal(created, found);
        Assert.Null(await other.GetSessionAsync(created.SessionId, CancellationToken.None));
        Assert.Null(await first.GetSessionAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>Uses a new local identity even for two sessions of the same host profile.</summary>
    [Fact]
    public async Task KeepsTwoSessionIdentitiesSeparate()
    {
        SessionCatalog catalog = CreateCatalog(new InMemorySessionStore());
        SessionExecutionContext workspace = new(Path.GetTempPath(), "workspace");
        SessionSnapshot first = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", workspace, CancellationToken.None);
        SessionSnapshot second = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", workspace, CancellationToken.None);

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(first, await catalog.GetSessionAsync(first.SessionId, CancellationToken.None));
        Assert.Equal(second, await catalog.GetSessionAsync(second.SessionId, CancellationToken.None));
    }

    /// <summary>Propagates cancellation to the store rather than returning a false not-found result.</summary>
    [Fact]
    public async Task CancelsReadsBeforeClaimingNotFound()
    {
        SessionCatalog catalog = CreateCatalog(new InMemorySessionStore());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.GetSessionAsync(Guid.NewGuid(), cancellation.Token).AsTask());
    }
}
