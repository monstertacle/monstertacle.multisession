using System.Security.Cryptography;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Cancellation after queue entry cannot silently commit startup recovery.</summary>
public sealed class StartupCancellationTests
{
    /// <summary>The host keeps ownership and may retry a cancelled, uncommitted preparation with a fresh token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelsStartedPreparationWithoutMutation(bool retry)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.DisposeAsync();
        byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(fixture.Host.Directory.DatabasePath));
        using CancellationTokenSource cancellation = new();
        await using CancellingStartupLease lease = new(fixture.Host.Directory, cancellation);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, fixture.Host.Directory.TrustScopeId, fixture.Host.Options, CancellationToken.None);
        lease.ArmAfterQueueCheck();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.StartAsync(lease, cancellation.Token).AsTask());
        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(lease.IsHeld);
        Assert.Equal((long)RequestState.Running, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT State FROM Requests"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM RecoveryCases"));
        if (retry)
        {
            await store.StartAsync(lease, CancellationToken.None);
            Assert.Equal(RequestState.Interrupted, (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!.State);
            Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
            Assert.Single((await store.ListRecoveryCasesAsync(10, null, CancellationToken.None)).Items);
        }
        await store.DisposeAsync();
        if (!retry) { Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(fixture.Host.Directory.DatabasePath))); }
    }
}
