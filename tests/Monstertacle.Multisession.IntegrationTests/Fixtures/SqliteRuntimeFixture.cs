using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed class SqliteRuntimeFixture(SessionRuntime runtime) : IAsyncDisposable
{
    internal SessionRuntime Runtime { get; } = runtime;
    internal static async Task<SqliteRuntimeFixture> OpenAsync(PrivateStoreDirectory directory, IReadOnlyList<ProviderRegistration> providers)
    {
        RetainedTestLease lease = new(directory);
        RuntimeOptions options = directory.Options with { MaxConcurrentRequests = 2 };
        ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, directory.TrustScopeId, options, CancellationToken.None);
        SessionRuntime runtime = new(store, lease, providers, []);
        try { await runtime.StartAsync(options, CancellationToken.None); return new(runtime); }
        catch { await store.DisposeAsync(); await lease.DisposeAsync(); throw; }
    }

    internal async Task<RequestSnapshot> CompletedAsync(Guid requestId)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            RequestSnapshot snapshot = (await Runtime.Requests.GetRequestAsync(requestId, deadline.Token))!;
            if (snapshot.State is not (RequestState.Queued or RequestState.Running)) { return snapshot; }
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
        }
    }
    public ValueTask DisposeAsync() => Runtime.DisposeAsync();
}
