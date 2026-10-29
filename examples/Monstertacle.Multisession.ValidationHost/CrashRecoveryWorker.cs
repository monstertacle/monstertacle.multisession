using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class CrashRecoveryWorker
{
    internal static async Task<int> RunAsync(string directory, string point)
    {
        // Parent initializes metadata; the trustscope is passed by its private marker, never inferred from another store.
        string scopeText = await File.ReadAllTextAsync(Path.Combine(directory, CrashProtocol.ScopeFileName));
        Guid scope = Guid.ParseExact(scopeText, "D");
        await using CrashStateDirectoryLease lease = new(directory);
        RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, scope, options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        CrashSessionProvider provider = new() { HoldBeforeObservation = point == CrashProtocol.BeforeOperationId };
        ProviderRegistration[] providers = [new(CrashProtocol.ProviderId, CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, provider)];
        SessionCatalog catalog = new(store, providers, [new("crash-profile", CrashProtocol.ProviderId, "opaque-test-agent", ExecutionKind.AgentTask, new("frozen", new Dictionary<string, string>()))]);
        SessionSnapshot a = await catalog.CreateSessionAsync("crash-profile", new(directory, "workspace-a"), CancellationToken.None);
        SessionSnapshot b = await catalog.CreateSessionAsync("crash-profile", new(directory, "workspace-b"), CancellationToken.None);
        RequestAdmission admission = new(store, options);
        RequestDispatcher dispatcher = new(store, providers, options);
        RequestReceipt completedA = await SubmitAsync(admission, a, "completed-a");
        if (!await dispatcher.TryDispatchAsync(CancellationToken.None)) { throw new InvalidOperationException("Missing setup dispatch."); }
        RequestReceipt completedB = await SubmitAsync(admission, b, "completed-b");
        if (!await dispatcher.TryDispatchAsync(CancellationToken.None)) { throw new InvalidOperationException("Missing setup dispatch."); }
        Guid candidate = Guid.NewGuid();
        CrashStateManifest basic = new(a.SessionId, b.SessionId, completedA.RequestId, completedB.RequestId, [], candidate);
        if (point == CrashProtocol.BeforeAcceptance) { return await HoldAsync(basic); }
        string input = point is CrashProtocol.RecoveryPoint or CrashProtocol.BeforeOperationId ? CrashProtocol.RunningInput : "boundary-b";
        await store.CommitAcceptanceAsync(new(candidate, new(b.SessionId, input, TimeSpan.FromMinutes(10), b.Context.ExecutionContext.WorkspaceKey, true)), 100, CancellationToken.None);
        if (point is CrashProtocol.AfterAcceptance or CrashProtocol.BeforeWake) { return await HoldAsync(basic); }
        if (point is CrashProtocol.AfterReservation or CrashProtocol.BeforeCompletion or CrashProtocol.AfterCompletion)
        {
            ExecutionReservation reservation = (await store.TryReserveHeadAsync(2, CancellationToken.None))!;
            if (point == CrashProtocol.AfterReservation) { return await HoldAsync(basic); }
            ReservedDispatch dispatch = (await store.GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None))!;
            ProviderReply reply = await provider.SendAsync(new(dispatch.Session.Context, candidate, reservation.DispatchId, input, dispatch.Submission.Timeout, options.MaxOutputBytes), dispatch.Session.Binding!,
                (observation, token) => store.CommitObservationAsync(reservation.DispatchId, observation, token), CancellationToken.None);
            if (point == CrashProtocol.BeforeCompletion) { return await HoldAsync(basic); }
            await store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None);
            return await HoldAsync(basic);
        }
        Task<bool> running = dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await provider.Running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        List<Guid> queued = [];
        if (point == CrashProtocol.RecoveryPoint)
        {
            for (int index = 0; index < 3; index++) { queued.Add((await SubmitAsync(admission, a, "queued-a-" + index)).RequestId); }
        }
        await SignalAsync(basic with { QueuedA = queued });
        await running;
        return 2;
    }

    private static ValueTask<RequestReceipt> SubmitAsync(RequestAdmission admission, SessionSnapshot session, string input)
        => admission.SubmitAsync(new(session.SessionId, input, TimeSpan.FromMinutes(10), session.Context.ExecutionContext.WorkspaceKey, true), CancellationToken.None);

    private static async Task<int> HoldAsync(CrashStateManifest manifest)
    {
        await SignalAsync(manifest);
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 2;
    }

    private static async Task SignalAsync(CrashStateManifest manifest)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new CrashSignal(CrashProtocol.RecoveryBarrier, Environment.ProcessId, DateTimeOffset.UtcNow) { State = manifest }));
        await Console.Out.FlushAsync();
    }
}
