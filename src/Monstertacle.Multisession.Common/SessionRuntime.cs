using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession;

/// <summary>In-process host composition with explicit store, ownership and provider configuration.</summary>
public sealed class SessionRuntime : IAsyncDisposable
{
    private readonly RuntimeLifecycle _lifecycle;

    /// <summary>Freezes host configuration. Store/lease lifetime transfers only after successful preparation.</summary>
    public SessionRuntime(ISessionStore store, IStateDirectoryLease lease, IReadOnlyList<ProviderRegistration> providers,
        IReadOnlyList<AgentProfile> profiles, TimeProvider? clock = null, SessionDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(profiles);
        ProviderCatalog configuration = new(providers, profiles);
        _lifecycle = new(new(store, lease, configuration.Providers, configuration.Profiles, clock ?? TimeProvider.System, diagnostics));
    }
    /// <summary>Gets session operations once the runtime has started.</summary>
    public ISessions Sessions => _lifecycle.Components.Facades.Sessions;
    /// <summary>Gets request operations once the runtime has started.</summary>
    public IRequests Requests => _lifecycle.Components.Facades.Requests;
    /// <summary>Gets explicit recovery operations once the runtime has started.</summary>
    public IRecovery Recovery => _lifecycle.Components.Facades.Recovery;
    /// <summary>Gets the observed lifetime of the bounded background workers.</summary>
    public Task Completion => _lifecycle.Completion;
    /// <summary>Gets the typed command dispatcher after startup.</summary>
    public Monstertacle.SharpQrs.ICommandDispatcher Commands => _lifecycle.Components.Dispatch.Commands;
    /// <summary>Gets the typed query dispatcher after startup.</summary>
    public Monstertacle.SharpQrs.IQueryDispatcher Queries => _lifecycle.Components.Dispatch.Queries;
    /// <summary>Starts validated ownership and bounded execution workers.</summary>
    public ValueTask StartAsync(RuntimeOptions options, CancellationToken cancellationToken) => _lifecycle.StartAsync(options, cancellationToken);
    /// <summary>Executes only the named eligible head in a host-controlled runtime, awaiting its durable result.</summary>
    public ValueTask<bool> DispatchAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
        return _lifecycle.RunOperation(token => _lifecycle.Components.Options.DispatchMode == RuntimeDispatchMode.HostControlled
            ? _lifecycle.Components.Dispatcher.TryDispatchAsync(token, requestId)
            : throw new SessionOperationException(new(ErrorCode.InvalidConfiguration)), cancellationToken);
    }
    /// <summary>Stops acceptance and preserves visible durable state.</summary>
    public ValueTask StopAsync(TimeSpan deadline, CancellationToken cancellationToken) => _lifecycle.StopAsync(deadline, cancellationToken);
    /// <summary>Releases resources after controlled stop, without deleting stored state.</summary>
    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();
}
