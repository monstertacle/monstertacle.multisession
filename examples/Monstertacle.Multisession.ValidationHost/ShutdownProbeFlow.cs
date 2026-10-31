using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class ShutdownProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(1);
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            try { await barrier.WaitAsync(CancellationToken.None); return ProbeReplies.Success(call, binding); }
            finally { finished.TrySetResult(); }
        }
        ProbeProvider provider = new("probe", trace, SendAsync);
        RuntimeOptions options = new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 1 };
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)], options);
        SessionSnapshot session = await host.CreateAsync("probe");
        RequestReceipt running = await host.SubmitAsync(session.SessionId, "uncooperative-running");
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        RequestReceipt queuedA = await host.SubmitAsync(session.SessionId, "queued-a");
        RequestReceipt queuedB = await host.SubmitAsync(session.SessionId, "queued-b");
        try
        {
            long began = Stopwatch.GetTimestamp();
            await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            ValidationCheck.Require(Stopwatch.GetElapsedTime(began) <= TimeSpan.FromSeconds(10) && !state.Lease.IsHeld, "ShutdownExceededDeadline");
        }
        finally { barrier.Release.TrySetResult(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using NativeLease lease = NativeLease.OpenExisting(state.Directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, state.TrustScope, options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        foreach (RequestReceipt queued in new[] { queuedA, queuedB })
        {
            RequestSnapshot request = await store.GetRequestAsync(queued.RequestId, CancellationToken.None) ?? throw new ValidationProbeException("LostQueuedIdentity");
            ValidationCheck.Require(request.State == RequestState.Cancelled && request.Certainty == ExecutionCertainty.NotStarted, "QueuedWorkWasNotCancelledBeforeStart");
        }
        RequestSnapshot uncertain = await store.GetRequestAsync(running.RequestId, CancellationToken.None) ?? throw new ValidationProbeException("LostRunningIdentity");
        ValidationCheck.Require(uncertain.State is not (RequestState.Queued or RequestState.Running or RequestState.Succeeded)
            && uncertain.Certainty == ExecutionCertainty.Unknown && trace.Calls.Count == 1, "LateSuccessOrReplayAfterShutdown");
        RuntimeStatistics stats = await store.GetRuntimeStatisticsAsync(CancellationToken.None);
        ValidationCheck.Require(stats.OccupiedReservations == 1 && stats.UncertainReservations == 1, "UnknownReservationWasReleased");
    }
}
