using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

/// <summary>In-process host composition with explicit store, ownership and provider configuration.</summary>
public sealed class SessionRuntime : IAsyncDisposable
{
    private readonly RuntimeLifecycle _lifecycle;

    /// <summary>Freezes host configuration. Store/lease lifetime transfers only after successful preparation.</summary>
    public SessionRuntime(ISessionStore store, IStateDirectoryLease lease, IReadOnlyList<ProviderRegistration> providers,
        IReadOnlyList<AgentProfile> profiles, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(profiles);
        AgentProfile[] frozenProfiles = profiles.Select(profile => profile with
        {
            Settings = profile.Settings with { Values = profile.Settings.Values.ToFrozenDictionary(StringComparer.Ordinal) },
        }).ToArray();
        _lifecycle = new(new(store, lease, providers.ToArray(), frozenProfiles, clock ?? TimeProvider.System));
    }
    /// <summary>Gets session operations once the runtime has started.</summary>
    public ISessions Sessions => _lifecycle.Components.Sessions;
    /// <summary>Gets request operations once the runtime has started.</summary>
    public IRequests Requests => _lifecycle.Components.Requests;
    /// <summary>Gets explicit recovery operations once the runtime has started.</summary>
    public IRecovery Recovery => _lifecycle.Components.Recovery;
    /// <summary>Gets the observed lifetime of the bounded background workers.</summary>
    public Task Completion => _lifecycle.Completion;
    /// <summary>Starts validated ownership and bounded execution workers.</summary>
    public ValueTask StartAsync(RuntimeOptions options, CancellationToken cancellationToken) => _lifecycle.StartAsync(options, cancellationToken);
    /// <summary>Stops acceptance and preserves visible durable state.</summary>
    public ValueTask StopAsync(TimeSpan deadline, CancellationToken cancellationToken) => _lifecycle.StopAsync(deadline, cancellationToken);
    /// <summary>Releases resources after controlled stop, without deleting stored state.</summary>
    public ValueTask DisposeAsync() => _lifecycle.DisposeAsync();
}
