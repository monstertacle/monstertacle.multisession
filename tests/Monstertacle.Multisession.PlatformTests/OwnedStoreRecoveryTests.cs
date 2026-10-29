using System.Runtime.Versioning;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Linux;
using Monstertacle.Multisession.PlatformTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Actual process exclusion and kill recovery with native Linux ownership and real SQLite state.</summary>
[SupportedOSPlatform("linux")]
public sealed class OwnedStoreRecoveryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), OwnershipProtocol.DirectoryPrefix + Guid.NewGuid().ToString("N"));
    private readonly Guid _scope = Guid.NewGuid();
    private readonly string _token = Guid.NewGuid().ToString("N");
    private RuntimeOptions Options => new() { StateDirectory = _path, Retention = new(true, null) };
    /// <summary>Removes only the newly created test store.</summary>
    public void Dispose() { if (Directory.Exists(_path)) { Directory.Delete(_path, true); } }

    /// <summary>An independently started .NET host is rejected while the parent's native lease is held.</summary>
    [Fact]
    public async Task SecondProcessReceivesStoreAlreadyOwned()
    {
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(_path);
        await WriteMarkerAsync(lease);
        await using OwnershipProcess child = await OwnershipProcess.StartAsync(_path, _token, _scope);
        Assert.False(child.Signal!.Acquired);
        Assert.Equal(ErrorCode.StoreAlreadyOwned, child.Signal.Failure);
        Assert.True(lease.IsHeld);
    }

    /// <summary>Killing the host releases its native lock while the persisted unknown reservation remains occupied.</summary>
    [Fact]
    public async Task KillAllowsReacquisitionWithoutLosingUnknownReservation()
    {
        Guid requestId = Guid.NewGuid();
        ExecutionReservation original;
        await using (LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(_path))
        {
            await WriteMarkerAsync(lease);
            await using ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, _scope, Options, CancellationToken.None);
            await store.StartAsync(lease, CancellationToken.None);
            Guid sessionId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            SessionSnapshot session = new(sessionId, "owned-provider", "owned-profile",
                new(sessionId, "owned-model", ExecutionKind.AgentTask, new(null, "owned-workspace"), new Dictionary<string, string>()), null)
            { SettingsRevision = "frozen", CreatedUtc = now, UpdatedUtc = now };
            await store.CreateSessionAsync(session, CancellationToken.None);
            await store.CommitSessionBindingAsync(sessionId, new("owned-adapter", 1, "native-root", null), CancellationToken.None);
            await store.CommitAcceptanceAsync(new(requestId, new(sessionId, "preserve input", TimeSpan.FromMinutes(1), "owned-workspace", true)), 100, CancellationToken.None);
            original = (await store.TryReserveHeadAsync(1, CancellationToken.None))!;
            await store.MarkUncertainAsync(original.DispatchId, new(ErrorCode.Interrupted, ExecutionCertainty.Unknown, sessionId, requestId), CancellationToken.None);
            Assert.Equal((UnixFileMode)384, File.GetUnixFileMode(Path.Combine(_path, "multisession.db")));
        }
        await using OwnershipProcess child = await OwnershipProcess.StartAsync(_path, _token, _scope);
        Assert.True(child.Signal!.Acquired);
        string journal = Path.Combine(_path, "multisession.db-journal");
        Assert.True(File.Exists(journal));
        Assert.Equal((UnixFileMode)384, File.GetUnixFileMode(journal));
        Assert.Null(child.Signal.Failure);
        Assert.Equal(ErrorCode.StoreAlreadyOwned,
            Assert.Throws<SessionOperationException>(() => LinuxStateDirectoryLease.OpenExisting(_path)).Failure.Code);
        await child.KillAsync();
        await using LinuxStateDirectoryLease reopened = LinuxStateDirectoryLease.OpenExisting(_path);
        await using ISessionStore recovered = await SqliteStoreFactory.OpenExistingAsync(reopened, _scope, Options, CancellationToken.None);
        await recovered.StartAsync(reopened, CancellationToken.None);
        RequestSnapshot request = (await recovered.GetRequestAsync(requestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Interrupted, request.State);
        Assert.Equal(ExecutionCertainty.Unknown, request.Certainty);
        RecoveryCaseSnapshot recoveryCase = (await recovered.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, requestId), CancellationToken.None))!;
        Assert.Equal(original.DispatchId, recoveryCase.DispatchId);
        Assert.Equal(new RuntimeStatistics(0, 1, 1), await recovered.GetRuntimeStatisticsAsync(CancellationToken.None));
        HistoryEntry history = Assert.Single((await recovered.ReadHistoryAsync(original.SessionId, 100, null, CancellationToken.None)).Items);
        Assert.Equal(requestId, history.RequestId);
        Assert.Equal("preserve input", history.Input);
        Assert.Equal(ExecutionCertainty.Unknown, history.Result.Certainty);
    }

    private async Task WriteMarkerAsync(LinuxStateDirectoryLease lease)
    {
        await lease.PrepareFileAsync(OwnershipProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(_path, OwnershipProtocol.MarkerFileName), _token);
    }
}
