using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Runs ten actual forced child terminations with the exact SC-009 persisted state.</summary>
[Collection("Exclusive storage volume")]
public sealed class PersistenceRecoveryTests
{
    /// <summary>Every confirmed original identity survives; queued A continues FIFO and uncertain B is never sent again.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task PreservesExactStateAcrossForcedRestart(int iteration)
    {
        Assert.InRange(iteration, 0, 9);
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        await directory.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory.StateDirectory, CrashProtocol.ScopeFileName), directory.TrustScopeId.ToString("D"));
        await using CrashHarness child = await CrashHarness.StartAsync(directory, CrashProtocol.RecoveryPoint);
        CrashStateManifest manifest = child.Signal!.State!;
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest.QueuedA.Count);
        Assert.Equal(2L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Requests WHERE State=2"));
        Assert.Equal(3L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Requests WHERE State=0"));
        Assert.Equal(1L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Requests WHERE State=1"));
        await child.KillAsync();
        ConcurrentQueue<Guid> sent = new();
        ConcurrentQueue<string> inputs = new();
        AdmissionProvider provider = new()
        {
            Opening = _ => throw new InvalidOperationException("Resume must retain existing native identity."),
            Sending = (call, binding, observe, token) =>
            {
                sent.Enqueue(call.RequestId);
                inputs.Enqueue(call.Input);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "continued:" + call.Input, true,
                    binding with { ContinuationState = "head-" + call.RequestId.ToString("N") }, null));
            },
        };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(directory,
            [new(CrashProtocol.ProviderId, CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, provider)]);
        foreach (Guid requestId in manifest.QueuedA) { Assert.Equal(RequestState.Succeeded, (await host.CompletedAsync(requestId)).State); }
        Assert.Equal(manifest.QueuedA, sent.ToArray());
        Assert.Equal(Enumerable.Range(0, 3).Select(index => "queued-a-" + index), inputs.ToArray());
        RequestSnapshot a = (await host.Runtime.Requests.GetRequestAsync(manifest.CompletedA, CancellationToken.None))!;
        RequestSnapshot b = (await host.Runtime.Requests.GetRequestAsync(manifest.CompletedB, CancellationToken.None))!;
        RequestSnapshot interrupted = (await host.Runtime.Requests.GetRequestAsync(manifest.RunningB, CancellationToken.None))!;
        Assert.Equal("completed:completed-a", a.Result?.Output);
        Assert.Equal("completed:completed-b", b.Result?.Output);
        Assert.Equal(RequestState.Interrupted, interrupted.State);
        Assert.Equal(ExecutionCertainty.Unknown, interrupted.Certainty);
        Assert.DoesNotContain(manifest.RunningB, sent);
        Assert.Equal(1L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        Assert.Equal("workspace-b", StoreProbe.Scalar(directory.DatabasePath, "SELECT WorkspaceKey FROM Reservations WHERE ReleasedUtc IS NULL"));
        SessionSnapshot sessionA = (await host.Runtime.Sessions.GetSessionAsync(manifest.SessionA, CancellationToken.None))!;
        SessionSnapshot sessionB = (await host.Runtime.Sessions.GetSessionAsync(manifest.SessionB, CancellationToken.None))!;
        Assert.Equal("native-" + manifest.SessionA.ToString("N"), sessionA.Binding?.SessionReference);
        Assert.Equal("native-" + manifest.SessionB.ToString("N"), sessionB.Binding?.SessionReference);
        Assert.Equal(SessionBlockReason.RecoveryRequired, sessionB.BlockReason);
        RecoveryCaseSnapshot recovery = (await host.Runtime.Recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, manifest.RunningB), CancellationToken.None))!;
        Assert.Equal("operation-" + manifest.RunningB.ToString("N"), recovery.OperationReference);
        Assert.Equal(6L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(0L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Requests WHERE State IN (0,1)"));
    }
}
