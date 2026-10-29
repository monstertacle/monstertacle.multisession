using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class RecoveryProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        for (int iteration = 0; iteration < 10; iteration++)
        {
            Console.Error.WriteLine($"Recovery phase=setup iteration={iteration} pid={Environment.ProcessId}");
            string directory = Path.Combine(state.Directory, "multisession-recovery-" + iteration);
            await using ValidationRunState setup = new(Guid.NewGuid(), NativeLease.CreateNew(directory));
            RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
            await (await SqliteStoreFactory.CreateNewAsync(setup.Lease, setup.TrustScope, options, CancellationToken.None)).DisposeAsync();
            await setup.Lease.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.CreateNew, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(directory, CrashProtocol.ScopeFileName), setup.TrustScope.ToString("D"));
            await using RecoveryProbeChild child = await RecoveryProbeChild.StartAsync(setup);
            Console.Error.WriteLine($"Recovery phase=barrier iteration={iteration} pid={Environment.ProcessId}");
            await child.KillAsync();
            Console.Error.WriteLine($"Recovery phase=killed iteration={iteration} pid={Environment.ProcessId}");
            await using ValidationRunState reopened = new(setup.RunId, NativeLease.OpenExisting(directory), setup.TrustScope);
            Console.Error.WriteLine($"Recovery phase=reopened iteration={iteration} pid={Environment.ProcessId}");
            await VerifyResumedAsync(reopened, child.Manifest);
            Console.Error.WriteLine($"Recovery phase=verified iteration={iteration} pid={Environment.ProcessId}");
        }
    }

    private static async Task VerifyResumedAsync(ValidationRunState state, CrashStateManifest manifest)
    {
        ConcurrentQueue<Guid> sent = new();
        ConcurrentQueue<string> inputs = new();
        AdmissionProvider provider = new()
        {
            Opening = _ => throw new ValidationProbeException("RecoveryOpenedNewNativeSession"),
            Sending = (call, binding, _, _) =>
            {
                sent.Enqueue(call.RequestId);
                inputs.Enqueue(call.Input);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "continued:" + call.Input, true,
                    binding with { ContinuationState = "head-" + call.RequestId.ToString("N") }, null));
            },
        };
        await using ProbeHost host = await ProbeHost.StartAsync(state,
            [new(CrashProtocol.ProviderId, CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, provider)], configuredProfiles: [], reopen: true);
        foreach (Guid request in manifest.QueuedA)
        {
            ValidationCheck.Require((await host.EndAsync(request)).State == RequestState.Succeeded, "RecoveryQueuedRequestFailed");
        }
        ValidationCheck.Require(sent.SequenceEqual(manifest.QueuedA)
            && inputs.SequenceEqual(Enumerable.Range(0, 3).Select(index => "queued-a-" + index)), "RecoveryChangedQueuedIdentityOrFifo");
        RequestSnapshot a = await host.EndAsync(manifest.CompletedA);
        RequestSnapshot b = await host.EndAsync(manifest.CompletedB);
        RequestSnapshot interrupted = await host.EndAsync(manifest.RunningB);
        ValidationCheck.Require(a.Result?.Output == "completed:completed-a" && b.Result?.Output == "completed:completed-b"
            && interrupted.State == RequestState.Interrupted && interrupted.Certainty == ExecutionCertainty.Unknown, "RecoveryChangedConfirmedOrUnknownResult");
        await VerifyBindingAsync(host, manifest);
        ValidationCheck.Require((await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None)).UncertainReservations == 1, "RecoveryReleasedUnknownReservation");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    private static async Task VerifyBindingAsync(ProbeHost host, CrashStateManifest manifest)
    {
        SessionSnapshot? a = await host.Runtime.Sessions.GetSessionAsync(manifest.SessionA, CancellationToken.None);
        SessionSnapshot? b = await host.Runtime.Sessions.GetSessionAsync(manifest.SessionB, CancellationToken.None);
        RecoveryCaseSnapshot? recovery = await host.Runtime.Recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, manifest.RunningB), CancellationToken.None);
        ValidationCheck.Require(a?.Binding?.SessionReference == "native-" + manifest.SessionA.ToString("N")
            && b?.Binding?.SessionReference == "native-" + manifest.SessionB.ToString("N") && b.BlockReason == SessionBlockReason.RecoveryRequired
            && recovery?.OperationReference == "operation-" + manifest.RunningB.ToString("N"), "RecoveryReplacedNativeIdentity");
    }
}
