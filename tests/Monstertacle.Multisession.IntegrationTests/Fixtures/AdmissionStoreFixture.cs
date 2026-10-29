using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed class AdmissionStoreFixture(PrivateStoreDirectory directory, ISessionStore store, RuntimeOptions options) : IAsyncDisposable
{
    public PrivateStoreDirectory Directory { get; } = directory;
    public ISessionStore Store { get; private set; } = store;
    public RuntimeOptions Options { get; } = options;
    public AdmissionProvider Provider { get; } = new();
    public SessionCatalog Catalog => new(Store, [new("connection-I", "fixture-adapter", 7, Provider)],
        [new("profile-İ", "connection-I", "model-I-İ", ExecutionKind.ModelResponse,
            new("revision-İ", new Dictionary<string, string> { ["I"] = "frozen-İ\n🙂", ["i"] = "distinct" }))]);

    public static async Task<AdmissionStoreFixture> CreateAsync(int maxQueued = 100, int maxInputBytes = 1024 * 1024, int dbCapacity = 256, int maxOutputBytes = 4 * 1024 * 1024, RetentionPolicy? retention = null)
    {
        PrivateStoreDirectory directory = new();
        ISessionStore? store = null;
        try
        {
            RuntimeOptions options = directory.Options with { MaxQueuedRequests = maxQueued, MaxInputBytes = maxInputBytes, DbWorkQueueCapacity = dbCapacity, MaxOutputBytes = maxOutputBytes, Retention = retention ?? directory.Options.Retention };
            store = await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, options, CancellationToken.None);
            await store.StartAsync(directory, CancellationToken.None);
            return new(directory, store, options);
        }
        catch
        {
            if (store is not null) { await store.DisposeAsync(); }
            await directory.DisposeAsync();
            throw;
        }
    }

    public ValueTask<SessionSnapshot> CreateSessionAsync()
        => Catalog.CreateSessionAsync("profile-İ", new(null, "host-workspace"), CancellationToken.None);

    public async Task ReopenAsync()
    {
        await Store.DisposeAsync();
        Store = await SqliteStoreFactory.OpenExistingAsync(Directory, Directory.TrustScopeId, Options, CancellationToken.None);
        await Store.StartAsync(Directory, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        await Directory.DisposeAsync();
    }
}
