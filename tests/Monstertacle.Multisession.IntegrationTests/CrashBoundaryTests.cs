using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Exercises real kill boundaries without inserting hooks into product transaction code.</summary>
public sealed class CrashBoundaryTests
{
    /// <summary>Only durably queued, never-started input may be sent after a forced termination.</summary>
    [Theory]
    [InlineData(CrashProtocol.BeforeAcceptance, null)]
    [InlineData(CrashProtocol.AfterAcceptance, RequestState.Succeeded)]
    [InlineData(CrashProtocol.BeforeWake, RequestState.Succeeded)]
    [InlineData(CrashProtocol.AfterReservation, RequestState.Interrupted)]
    [InlineData(CrashProtocol.BeforeOperationId, RequestState.Interrupted)]
    [InlineData(CrashProtocol.BeforeCompletion, RequestState.Interrupted)]
    [InlineData(CrashProtocol.AfterCompletion, RequestState.Succeeded)]
    public async Task PreservesDurabilityAndDoesNotRepeatAmbiguousExecution(string point, RequestState? expected)
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        await directory.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory.StateDirectory, CrashProtocol.ScopeFileName), directory.TrustScopeId.ToString("D"));
        await using CrashHarness child = await CrashHarness.StartAsync(directory, point);
        CrashStateManifest state = child.Signal!.State!;
        Assert.NotNull(state);
        await child.KillAsync();
        ConcurrentQueue<Guid> sent = new();
        AdmissionProvider provider = new()
        {
            Sending = (call, binding, observe, token) =>
            {
                sent.Enqueue(call.RequestId);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "resumed", true, binding, null));
            },
        };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(directory, [new(CrashProtocol.ProviderId, CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, provider)]);
        bool queuedBeforeKill = point is CrashProtocol.AfterAcceptance or CrashProtocol.BeforeWake;
        RequestSnapshot? candidate = queuedBeforeKill ? await host.CompletedAsync(state.RunningB)
            : await host.Runtime.Requests.GetRequestAsync(state.RunningB, CancellationToken.None);
        Assert.Equal(expected, candidate?.State);
        Assert.Equal(queuedBeforeKill ? 1 : 0, sent.Count);
        if (queuedBeforeKill) { Assert.Equal(state.RunningB, Assert.Single(sent)); }
        if (expected == RequestState.Interrupted)
        {
            Assert.Equal(ExecutionCertainty.Unknown, candidate?.Certainty);
            Assert.Equal(1L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
            RecoveryCaseSnapshot recovery = (await host.Runtime.Recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, state.RunningB), CancellationToken.None))!;
            Assert.Null(recovery.OperationReference);
        }
        else { Assert.Equal(0L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL")); }
        if (point == CrashProtocol.AfterCompletion) { Assert.Equal("completed:boundary-b", candidate?.Result?.Output); }
        Assert.Equal(RequestState.Succeeded, (await host.Runtime.Requests.GetRequestAsync(state.CompletedA, CancellationToken.None))?.State);
        Assert.Equal(RequestState.Succeeded, (await host.Runtime.Requests.GetRequestAsync(state.CompletedB, CancellationToken.None))?.State);
    }
}
