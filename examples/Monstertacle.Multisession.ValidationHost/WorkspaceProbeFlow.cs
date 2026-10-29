using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class WorkspaceProbeFlow
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
        await using ProbeHost host = await ProbeHost.StartAsync(state, [new("probe", "probe", 1, provider)], kind: ExecutionKind.AgentTask);
        SessionSnapshot first = await host.CreateAsync("probe", "shared");
        SessionSnapshot second = await host.CreateAsync("probe", "shared");
        SessionSnapshot independent = await host.CreateAsync("probe", "independent");
        RequestReceipt a = await host.SubmitAsync(first.SessionId, "first-shared", "shared");
        RequestReceipt b = await host.SubmitAsync(second.SessionId, "second-shared", "shared");
        RequestReceipt c = await host.SubmitAsync(independent.SessionId, "independent", "independent");
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ProbeInvocation[] held = trace.Calls.ToArray();
            ValidationCheck.Require(held.Length == 2 && held.Select(item => item.Call.Session.ExecutionContext.WorkspaceKey).Distinct(StringComparer.Ordinal).Count() == 2
                && held.All(item => item.Call.RequestId != b.RequestId), "OverlappingSharedWorkspace");
            ValidationCheck.Require((await host.Runtime.Requests.GetRequestAsync(b.RequestId, CancellationToken.None))?.State == RequestState.Queued, "SharedRequestDidNotWait");
        }
        finally { barrier.Release.TrySetResult(); }
        foreach (RequestReceipt receipt in new[] { a, b, c }) { ValidationCheck.Require((await host.EndAsync(receipt.RequestId)).State == RequestState.Succeeded, "WorkspaceRequestDidNotFinish"); }
        ValidationCheck.Require(trace.Calls.Count == 3 && trace.Calls.Where(item => item.Call.Session.ExecutionContext.WorkspaceKey == "shared")
            .Select(item => item.Call.RequestId).SequenceEqual(new[] { a.RequestId, b.RequestId }), "WrongWorkspaceOrder");
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}
