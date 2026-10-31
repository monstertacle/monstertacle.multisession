using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Executes the consumer-owned adapter contract through public runtime/store APIs only.
internal static class ConsumerProbeFlow
{
    internal static async Task ExecuteAsync(ValidationRunState state)
    {
        RuntimeOptions options = new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        ConsumerOwnedProvider provider = new();
        await using ISessionStore store = await SqliteStoreFactory.CreateNewAsync(state.Lease, state.TrustScope, options, CancellationToken.None);
        await using SessionRuntime runtime = new(store, state.Lease, [new("consumer", "consumer-owned", 1, provider)],
            [new("profile", "consumer", "opaque-consumer-model", ExecutionKind.ModelResponse, new("frozen", new Dictionary<string, string>()))]);
        await runtime.StartAsync(options, CancellationToken.None);
        SessionSnapshot a = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        SessionSnapshot b = await runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        RequestReceipt firstA = await SubmitAsync(runtime, a.SessionId, "first-a");
        RequestReceipt firstB = await SubmitAsync(runtime, b.SessionId, "first-b");
        try
        {
            await provider.BothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ValidationCheck.Require((await runtime.Requests.GetRequestAsync(firstA.RequestId, CancellationToken.None))?.State == RequestState.Running
                && (await runtime.Requests.GetRequestAsync(firstB.RequestId, CancellationToken.None))?.State == RequestState.Running, "MissingParallelSends");
        }
        finally { provider.ReleaseFirsts(); }
        ValidationCheck.Require((await EndAsync(runtime, firstA.RequestId)).Result?.Output == "consumer:first-a", "WrongFirstOutput");
        ValidationCheck.Require((await EndAsync(runtime, firstB.RequestId)).Result?.Output == "consumer:first-b", "WrongFirstOutput");
        RequestReceipt followA = await SubmitAsync(runtime, a.SessionId, "follow-a");
        RequestReceipt followB = await SubmitAsync(runtime, b.SessionId, "follow-b");
        ValidationCheck.Require((await EndAsync(runtime, followA.RequestId)).Result?.Output == "consumer:follow-a", "WrongFollowOutput");
        ValidationCheck.Require((await EndAsync(runtime, followB.RequestId)).Result?.Output == "consumer:follow-b", "WrongFollowOutput");
        RequestReceipt delayed = await SubmitAsync(runtime, a.SessionId, "delayed");
        ValidationCheck.Require(await provider.DelayedEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)) == delayed.RequestId, "WrongDelayedIdentity");
        long began = Stopwatch.GetTimestamp();
        await runtime.Requests.CancelRequestAsync(delayed.RequestId, CancellationToken.None);
        RequestSnapshot cancelled = await EndAsync(runtime, delayed.RequestId);
        ValidationCheck.Require(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(2)
            && cancelled.State == RequestState.Cancelled && cancelled.Certainty == ExecutionCertainty.ConfirmedEnded, "CancellationNotConfirmedInTime");
        ValidationCheck.Require(provider.Inputs.Count == 5, "UnexpectedProviderCalls");
        Page<HistoryEntry> history = await runtime.Sessions.ReadHistoryAsync(b.SessionId, 100, null, CancellationToken.None);
        ValidationCheck.Require(history.Items.Count == 2 && history.Items[0].Input == "first-b" && history.Items[1].Input == "follow-b", "WrongHistory");
        await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        ValidationCheck.Require(runtime.Completion.IsCompletedSuccessfully && !state.Lease.IsHeld, "RuntimeNotStopped");
        await VerifyStoredAsync(state, options, [firstA.RequestId, firstB.RequestId, followA.RequestId, followB.RequestId, delayed.RequestId]);
    }

    private static async Task VerifyStoredAsync(ValidationRunState state, RuntimeOptions options, IReadOnlyList<Guid> requests)
    {
        await using NativeLease lease = NativeLease.OpenExisting(state.Directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, state.TrustScope, options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        foreach (Guid id in requests) { ValidationCheck.Require(await store.GetRequestAsync(id, CancellationToken.None) is not null, "MissingPersistedRequest"); }
        ValidationCheck.Require((await store.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations == 0, "UnexpectedReservation");
    }
    private static ValueTask<RequestReceipt> SubmitAsync(SessionRuntime runtime, Guid session, string input)
        => runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session, input, TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
    private static async Task<RequestSnapshot> EndAsync(SessionRuntime runtime, Guid id)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            RequestSnapshot request = await runtime.Requests.GetRequestAsync(id, deadline.Token) ?? throw new ValidationProbeException("MissingAcceptedRequest");
            if (request.State is not (RequestState.Queued or RequestState.Running)) { return request; }
            await Task.Delay(10, deadline.Token);
        }
    }
}
