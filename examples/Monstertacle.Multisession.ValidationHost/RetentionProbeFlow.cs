using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class RetentionProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        ProbeTrace trace = new();
        ProbeBarrier barrier = new(1);
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<ProviderReply> SendAsync(ProviderCall call, ProviderBinding binding, CancellationToken token)
        {
            if (call.Input != "unknown") { return ProbeReplies.Success(call, binding); }
            try { await barrier.WaitAsync(CancellationToken.None); return ProbeReplies.Success(call, binding); }
            finally { finished.TrySetResult(); }
        }
        ProbeProvider provider = new("probe", trace, SendAsync);
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)]);
        SessionSnapshot deletable = await host.CreateAsync("probe");
        SessionSnapshot retained = await host.CreateAsync("probe");
        RequestReceipt completed = await host.SubmitAsync(deletable.SessionId, "completed");
        ValidationCheck.Require((await host.EndAsync(completed.RequestId)).State == RequestState.Succeeded, "MissingRetentionSetupResult");
        await host.Runtime.Sessions.CloseSessionAsync(deletable.SessionId, CancellationToken.None);
        await host.Runtime.Sessions.DeleteSessionAsync(deletable.SessionId, CancellationToken.None);
        ValidationCheck.Require(await host.Runtime.Sessions.GetSessionAsync(deletable.SessionId, CancellationToken.None) is null
            && await host.Runtime.Requests.GetRequestAsync(completed.RequestId, CancellationToken.None) is null, "ClosedContentsWereNotDeleted");
        RequestReceipt unknown = await host.SubmitAsync(retained.SessionId, "unknown");
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Runtime.Requests.CancelRequestAsync(unknown.RequestId, CancellationToken.None);
            ValidationCheck.Require((await host.EndAsync(unknown.RequestId)).Certainty == ExecutionCertainty.Unknown, "UnconfirmedEndWasInvented");
            await host.Runtime.Sessions.CloseSessionAsync(retained.SessionId, CancellationToken.None);
            await RefuseDeleteAsync(host, retained.SessionId);
            ValidationCheck.Require(await host.Runtime.Requests.GetRequestAsync(unknown.RequestId, CancellationToken.None) is not null
                && (await host.Store.GetRuntimeStatisticsAsync(CancellationToken.None)).UncertainReservations == 1, "UncertainWorkWasDeleted");
        }
        finally { barrier.Release.TrySetResult(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
    private static async Task RefuseDeleteAsync(ProbeHost host, Guid session)
    {
        try { await host.Runtime.Sessions.DeleteSessionAsync(session, CancellationToken.None); }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.SessionBlocked) { return; }
        throw new ValidationProbeException("UncertainSessionDeletionWasNotRejected");
    }
}
