using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;

namespace Monstertacle.Multisession.ValidationHost;

internal static class ExtensionsProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(1);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            await barrier.WaitAsync(token);
            return ProbeReplies.Success(call, binding);
        }
        ProbeProvider model = new("model-contract", trace, SendAsync);
        ThirdContractProvider external = new(barrier.Release.Task);
        ProbeHost host = await ProbeHost.StartAsync(state,
            [new("model", "model-contract", 1, model), new("external", "third-contract", 7, external)], kind: ExecutionKind.AgentTask);
        await ExecuteOwnedAsync(host, trace, barrier, external, state.Directory);
    }

    internal static async Task ExecuteOwnedAsync(ProbeHost host, ProbeTrace trace, ProbeBarrier barrier, ThirdContractProvider external, string directory)
    {
        try { await ExecuteWorkloadAsync(host, trace, barrier, external, directory); }
        catch (Exception primary)
        {
            // A disposal failure is added as cleanup and never replaces the workload failure (same owner rule as WaveProbeFlow).
            try { await host.DisposeAsync(); }
            catch (Exception cleanup) { throw ValidationProbeException.FromException(primary).WithCleanup(cleanup); }
            throw;
        }
        // The workload ends with its own explicit stop, which already ran runtime cleanup; disposal then finds a stopped runtime.
        await host.DisposeAsync();
    }

    private static async Task ExecuteWorkloadAsync(ProbeHost host, ProbeTrace trace, ProbeBarrier barrier, ThirdContractProvider external, string directory)
    {
        SessionSnapshot a = await host.CreateAsync("model");
        SessionSnapshot b = await host.CreateAsync("external", "external-workspace", directory);
        RequestReceipt first = await host.SubmitAsync(a.SessionId, "model-first");
        RequestReceipt externalFirst = await host.SubmitAsync(b.SessionId, "external-first", "external-workspace");
        try
        {
            await Task.WhenAll(barrier.Entered.Task, external.Entered.Task).WaitAsync(TimeSpan.FromSeconds(5));
            ValidationCheck.Require((await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations == 2, "DifferentContractsDidNotOverlap");
        }
        finally { barrier.Release.TrySetResult(); }
        ValidationCheck.Require((await host.EndAsync(first.RequestId)).State == RequestState.Succeeded
            && (await host.EndAsync(externalFirst.RequestId)).Result?.Output == "external-first", "DifferentContractFirstTurnFailed");
        RequestReceipt follow = await host.SubmitAsync(b.SessionId, "external-follow", "external-workspace");
        ValidationCheck.Require((await host.EndAsync(follow.RequestId)).Result?.Output == "external-follow"
            && external.Calls.Count == 2 && trace.Calls.Count == 1, "DifferentContractFollowTurnFailed");
        SessionSnapshot current = await host.Runtime.Sessions.GetSessionAsync(b.SessionId, CancellationToken.None) ?? throw new ValidationProbeException("MissingExternalSession");
        ValidationCheck.Require(current.Binding?.AdapterId == "third-contract" && current.Binding.FormatVersion == 7
            && current.Binding.SessionReference == b.Binding?.SessionReference && current.Binding.ContinuationState?.StartsWith("resume/", StringComparison.Ordinal) == true,
            "ThirdContractIdentityWasReplaced");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}
