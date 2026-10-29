using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class CapacityProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(2);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            await barrier.WaitAsync(token);
            return ProbeReplies.Success(call, binding);
        }
        ProbeProvider provider = new("probe", trace, SendAsync);
        RuntimeOptions options = new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 2, MaxQueuedRequests = 3 };
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)], options);
        List<SessionSnapshot> sessions = [];
        for (int index = 0; index < 10; index++) { sessions.Add(await host.CreateAsync("probe")); }
        List<RequestReceipt> accepted = [await host.SubmitAsync(sessions[0].SessionId, "running-0"), await host.SubmitAsync(sessions[1].SessionId, "running-1")];
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            AdmissionAttempt[] attempts = await Task.WhenAll(sessions.Skip(2).Select(session => TrySubmitAsync(host, session.SessionId)));
            accepted.AddRange(attempts.Where(item => item.Receipt is not null).Select(item => item.Receipt!));
            ValidationCheck.Require(accepted.Count == 5 && attempts.Count(item => item.Failure?.Code == ErrorCode.QueueFull
                && item.Failure.AdmissionStatus == AdmissionStatus.NotAccepted) == 5, "WrongConcurrentCapacityAdmission");
            RuntimeStatistics stats = await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None);
            ValidationCheck.Require(stats.QueuedRequests == 3 && stats.OccupiedReservations == 2 && trace.Calls.Count == 2, "WrongHeldCapacity");
        }
        finally { barrier.Release.TrySetResult(); }
        foreach (RequestReceipt receipt in accepted) { ValidationCheck.Require((await host.EndAsync(receipt.RequestId)).State == RequestState.Succeeded, "AcceptedRequestDidNotFinish"); }
        ValidationCheck.Require(trace.Calls.Count == 5, "RejectedRequestWasSent");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
    private static async Task<AdmissionAttempt> TrySubmitAsync(ProbeHost host, Guid session)
    {
        try { return new(await host.SubmitAsync(session, "parallel-candidate"), null); }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.QueueFull) { return new(null, failure.Failure); }
    }
}

internal sealed record AdmissionAttempt(RequestReceipt? Receipt, PublicError? Failure);
