using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class CrashRecoveryWorker
{
    internal static Task<int> RunAsync(IStateDirectoryLease lease, string point) => RunAsync(lease, point, new CrashStepClock());

    // Every setup step is timed by the clock; the signal and a failure line carry the measured steps.
    internal static async Task<int> RunAsync(IStateDirectoryLease lease, string point, CrashStepClock clock)
    {
        // Parent initializes metadata; the trustscope is passed by its private marker, never inferred from another store.
        string directory = lease.StateDirectory;
        Guid scope = await clock.MeasureAsync("lease-files", async () =>
        {
            await lease.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.OpenExisting, CancellationToken.None);
            await lease.PrepareFileAsync(CrashProtocol.MarkerFileName, StateFileMode.OpenExisting, CancellationToken.None);
            return Guid.ParseExact(await File.ReadAllTextAsync(Path.Combine(directory, CrashProtocol.ScopeFileName)), "D");
        });
        RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        await using ISessionStore store = await clock.MeasureAsync("store-open", () => SqliteStoreFactory.OpenExistingAsync(lease, scope, options, CancellationToken.None).AsTask());
        await clock.MeasureAsync("store-start", () => store.StartAsync(lease, CancellationToken.None).AsTask());
        CrashSessionProvider provider = new() { HoldBeforeObservation = point == CrashProtocol.BeforeOperationId };
        ProviderRegistration[] providers = [new(CrashProtocol.ProviderId, CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, provider)];
        SessionCatalog catalog = new(store, providers, [new("crash-profile", CrashProtocol.ProviderId, "opaque-test-agent", ExecutionKind.AgentTask, new("frozen", new Dictionary<string, string>()))]);
        SessionSnapshot a = await clock.MeasureAsync("session-a", () => catalog.CreateSessionAsync(Guid.NewGuid(), "crash-profile", new(directory, "workspace-a"), CancellationToken.None).AsTask());
        SessionSnapshot b = await clock.MeasureAsync("session-b", () => catalog.CreateSessionAsync(Guid.NewGuid(), "crash-profile", new(directory, "workspace-b"), CancellationToken.None).AsTask());
        RequestDispatcher dispatcher = new(store, providers, options);
        CrashSetupSteps steps = new(clock, new RequestAdmission(store, options), dispatcher);
        RequestReceipt completedA = await steps.SubmitAsync("completed-a", a, "completed-a");
        await steps.DispatchAsync("completed-a-dispatch");
        RequestReceipt completedB = await steps.SubmitAsync("completed-b", b, "completed-b");
        await steps.DispatchAsync("completed-b-dispatch");
        Guid candidate = Guid.NewGuid();
        CrashStateManifest basic = new(a.SessionId, b.SessionId, completedA.RequestId, completedB.RequestId, [], candidate);
        if (point == CrashProtocol.BeforeAcceptance) { return await steps.HoldAsync(basic); }
        string input = point is CrashProtocol.RecoveryPoint or CrashProtocol.BeforeOperationId ? CrashProtocol.RunningInput : "boundary-b";
        await clock.MeasureAsync("running-acceptance", () => store.CommitAcceptanceAsync(new(candidate, new(b.SessionId, input, TimeSpan.FromMinutes(10), b.Context.ExecutionContext.WorkspaceKey, true)), 100, CancellationToken.None).AsTask());
        if (point is CrashProtocol.AfterAcceptance or CrashProtocol.BeforeWake) { return await steps.HoldAsync(basic); }
        if (point is CrashProtocol.AfterReservation or CrashProtocol.BeforeCompletion or CrashProtocol.AfterCompletion)
        {
            ExecutionReservation reservation = (await store.TryReserveHeadAsync(2, CancellationToken.None))!;
            if (point == CrashProtocol.AfterReservation) { return await steps.HoldAsync(basic); }
            ReservedDispatch dispatch = (await store.GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None))!;
            ProviderReply reply = await provider.SendAsync(new(dispatch.Session.Context, candidate, reservation.DispatchId, input, dispatch.Submission.Timeout, options.MaxOutputBytes), dispatch.Session.Binding!,
                (observation, token) => store.CommitObservationAsync(reservation.DispatchId, observation, token), CancellationToken.None);
            if (point == CrashProtocol.BeforeCompletion) { return await steps.HoldAsync(basic); }
            await store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None);
            return await steps.HoldAsync(basic);
        }
        Task<bool> running = dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await clock.MeasureAsync("running-started", () => provider.Running.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        List<Guid> queued = [];
        if (point == CrashProtocol.RecoveryPoint)
        {
            for (int index = 0; index < 3; index++) { queued.Add((await steps.SubmitAsync("queued-a-" + index, a, "queued-a-" + index)).RequestId); }
        }
        await steps.SignalAsync(basic with { QueuedA = queued });
        await running;
        return 2;
    }
}
