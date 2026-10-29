using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal static class CrashRecoveryWorker
{
    internal static async Task<int> RunAsync(string directory)
    {
        // Parent initializes metadata; the trustscope is passed by its private marker, never inferred from another store.
        string scopeText = await File.ReadAllTextAsync(Path.Combine(directory, CrashProtocol.ScopeFileName));
        Guid scope = Guid.ParseExact(scopeText, "D");
        await using CrashStateDirectoryLease lease = new(directory);
        RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, scope, options, CancellationToken.None);
        await store.StartAsync(lease, CancellationToken.None);
        CrashSessionProvider provider = new();
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
        RequestReceipt runningB = await SubmitAsync(admission, b, CrashProtocol.RunningInput);
        Task<bool> running = dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await provider.Running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        List<Guid> queued = [];
        for (int index = 0; index < 3; index++) { queued.Add((await SubmitAsync(admission, a, "queued-a-" + index)).RequestId); }
        CrashStateManifest manifest = new(a.SessionId, b.SessionId, completedA.RequestId, completedB.RequestId, queued, runningB.RequestId);
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new CrashSignal(CrashProtocol.RecoveryBarrier, Environment.ProcessId, DateTimeOffset.UtcNow) { State = manifest }));
        await Console.Out.FlushAsync();
        await running;
        return 2;
    }

    private static ValueTask<RequestReceipt> SubmitAsync(RequestAdmission admission, SessionSnapshot session, string input)
        => admission.SubmitAsync(new(session.SessionId, input, TimeSpan.FromMinutes(10), session.Context.ExecutionContext.WorkspaceKey, true), CancellationToken.None);
}
