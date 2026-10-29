using System.Security.Cryptography;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Actual OS write protection cannot produce an unsaved acceptance or alter prior results.</summary>
public sealed class ReadOnlyStorageTests
{
    /// <summary>The read-write open flag cannot silently make a protected database writable.</summary>
    [Fact]
    public async Task RejectsOsWriteProtectedStoreWithoutChangingExistingData()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(fixture.Host.Directory.DatabasePath));
        using (ReadOnlyFile protection = new(fixture.Host.Directory.DatabasePath))
        {
            Assert.True(protection.DeniesNewWriteHandle());
            await using CrashStateDirectoryLease lease = new(fixture.Host.Directory.StateDirectory);
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => AttemptWriteAsync(lease, fixture));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Null(failure.InnerException);
            Assert.Equal(nameof(ErrorCode.StorageUnavailable), failure.Message);
            Assert.NotEqual(AdmissionStatus.Committed, failure.Failure.AdmissionStatus);
        }
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(fixture.Host.Directory.DatabasePath)));
        await fixture.Host.ReopenAsync();
        RequestSnapshot request = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Succeeded, request.State);
        Assert.Equal("complete output", request.Result?.Output);
        Assert.Single((await fixture.Host.Store.ReadHistoryAsync(fixture.Session.SessionId, 100, null, CancellationToken.None)).Items);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    private static async Task AttemptWriteAsync(CrashStateDirectoryLease lease, PersistedDispatchFixture fixture)
    {
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, fixture.Host.Directory.TrustScopeId, fixture.Host.Options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(fixture.Session.SessionId, "must not be accepted", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
    }
}
