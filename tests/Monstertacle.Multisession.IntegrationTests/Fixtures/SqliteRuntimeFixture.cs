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
        RequestSnapshot? latest = null;
        try
        {
            while (true)
            {
                latest = (await Runtime.Requests.GetRequestAsync(requestId, deadline.Token))!;
                if (latest.State is not (RequestState.Queued or RequestState.Running)) { return latest; }
                await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"Fixture request {requestId:D}: state={latest?.State}, certainty={latest?.Certainty}, error={latest?.Result?.Error?.Code}, workers={Runtime.Completion.Status}.");
        }
    }
    public ValueTask DisposeAsync() => Runtime.DisposeAsync();
}
