using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ProbeHost(ISessionStore store, SessionRuntime runtime, RuntimeOptions options) : IAsyncDisposable
{
    internal ISessionStore Store { get; } = store;
    internal SessionRuntime Runtime { get; } = runtime;
    internal RuntimeOptions Options { get; } = options;
    internal static async Task<ProbeHost> StartAsync(ValidationRunState state, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions? selected = null,
        ExecutionKind kind = ExecutionKind.ModelResponse, IReadOnlyList<AgentProfile>? configuredProfiles = null, bool reopen = false)
    {
        RuntimeOptions options = selected ?? new() { StateDirectory = state.Directory, Retention = new(true, null), MaxConcurrentRequests = 2 };
        ISessionStore store = reopen
            ? await SqliteStoreFactory.OpenExistingAsync(state.Lease, state.TrustScope, options, CancellationToken.None)
            : await SqliteStoreFactory.CreateNewAsync(state.Lease, state.TrustScope, options, CancellationToken.None);
        IReadOnlyList<AgentProfile> profiles = configuredProfiles ?? providers.Select(item => new AgentProfile(item.ProviderId, item.ProviderId, "model-" + item.ProviderId,
            kind, new("frozen", new Dictionary<string, string>()))).ToArray();
        SessionRuntime runtime = new(store, state.Lease, providers, profiles);
        return await StartPreparedAsync(store, runtime, options);
    }

    internal static async Task<ProbeHost> StartPreparedAsync(ISessionStore store, SessionRuntime runtime, RuntimeOptions options)
    {
        try { await runtime.StartAsync(options, CancellationToken.None); return new(store, runtime, options); }
        catch (Exception primary)
        {
            ValidationProbeException failure = ValidationProbeException.FromException(primary);
            try { await runtime.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure.WithCleanup(cleanup); }
            try { await store.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure.WithCleanup(cleanup); }
            if (failure.CleanupFailures.Count != 0) { throw failure; }
            throw;
        }
    }
    internal ValueTask<SessionSnapshot> CreateAsync(string profile, string? workspace = null, string? directory = null)
        => Runtime.Sessions.CreateSessionAsync(Guid.NewGuid(), profile, new(directory, workspace), CancellationToken.None);
    // Every scenario admission is recorded in the scenario's admission envelope (M1, test/ValidationHost only); the outcome and any
    // exception are returned or rethrown unchanged.
    internal async ValueTask<RequestReceipt> SubmitAsync(Guid session, string input, string? workspace = null)
    {
        ScenarioAdmissionTrace? trace = ScenarioAdmissionTrace.Current;
        (int, string, long)? admission = trace?.Start(input);
        try
        {
            RequestReceipt receipt = await Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session, input, TimeSpan.FromSeconds(10), workspace, workspace is not null), CancellationToken.None);
            if (admission is { } accepted) { trace!.Accepted(accepted, receipt.RequestId); }
            return receipt;
        }
        catch (Exception failure)
        {
            if (admission is { } failed) { trace!.Failed(failed, failure); }
            throw;
        }
    }
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
