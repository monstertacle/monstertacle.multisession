using System.Diagnostics;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class CancellationProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(1);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            try { await barrier.WaitAsync(token); return ProbeReplies.Success(call, binding); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return ProbeReplies.Cancelled(binding); }
        }
        ProbeProvider provider = new("probe", trace, SendAsync);
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)]);
        SessionSnapshot session = await host.CreateAsync("probe");
        RequestReceipt receipt = await host.SubmitAsync(session.SessionId, "held cooperative request");
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            long began = Stopwatch.GetTimestamp();
            await host.Runtime.Requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            RequestSnapshot ended = await host.EndAsync(receipt.RequestId);
            ValidationCheck.Require(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(2)
                && ended.State == RequestState.Cancelled && ended.Certainty == ExecutionCertainty.ConfirmedEnded, "CancellationNotConfirmedInTime");
            ValidationCheck.Require(trace.Calls.Count == 1 && (await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations == 0, "WrongCancellationOccupancy");
        }
        finally { barrier.Release.TrySetResult(); }
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}
