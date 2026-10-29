using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ProbeHost(ISessionStore store, SessionRuntime runtime, RuntimeOptions options) : IAsyncDisposable
{
    internal ISessionStore Store { get; } = store;
    internal SessionRuntime Runtime { get; } = runtime;
    internal RuntimeOptions Options { get; } = options;
    internal static async Task<ProbeHost> StartAsync(ValidationRunState state, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions? selected = null)
    {
        RuntimeOptions options = selected ?? new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        ISessionStore store = await SqliteStoreFactory.CreateNewAsync(state.Lease, state.TrustScope, options, CancellationToken.None);
        AgentProfile[] profiles = providers.Select(item => new AgentProfile(item.ProviderId, item.ProviderId, "model-" + item.ProviderId,
            ExecutionKind.ModelResponse, new("frozen", new Dictionary<string, string>()))).ToArray();
        SessionRuntime runtime = new(store, state.Lease, providers, profiles);
        try { await runtime.StartAsync(options, CancellationToken.None); return new(store, runtime, options); }
        catch { await runtime.DisposeAsync(); await store.DisposeAsync(); throw; }
    }
    internal ValueTask<SessionSnapshot> CreateAsync(string profile, string? workspace = null)
        => Runtime.Sessions.CreateSessionAsync(profile, new(null, workspace), CancellationToken.None);
    internal ValueTask<RequestReceipt> SubmitAsync(Guid session, string input, string? workspace = null)
        => Runtime.Requests.SubmitAsync(new(session, input, TimeSpan.FromSeconds(10), workspace, workspace is not null), CancellationToken.None);
    internal async Task<RequestSnapshot> EndAsync(Guid id)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        while (true)
        {
            RequestSnapshot current = await Runtime.Requests.GetRequestAsync(id, deadline.Token) ?? throw new ValidationProbeException("MissingAcceptedRequest");
            if (current.State is not (RequestState.Queued or RequestState.Running)) { return current; }
            await Task.Delay(10, deadline.Token);
        }
    }
    public ValueTask DisposeAsync() => Runtime.DisposeAsync();
}
