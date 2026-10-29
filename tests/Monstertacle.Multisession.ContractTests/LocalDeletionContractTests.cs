using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Public close and explicit deletion use host-selected policy through generic store facets.</summary>
public sealed class LocalDeletionContractTests
{
    /// <summary>The configured host can close and delete its own local session without an external provider action.</summary>
    [Fact]
    public async Task DeletesThroughPublicRuntimeFacades()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, new(), lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        await runtime.Sessions.CloseSessionAsync(session.SessionId, CancellationToken.None);
        await runtime.Sessions.DeleteSessionAsync(session.SessionId, CancellationToken.None);
        Assert.Null(await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None));
        Assert.Empty((await runtime.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items);
    }
}
