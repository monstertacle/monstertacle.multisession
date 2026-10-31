namespace Monstertacle.Multisession.Contracts;

/// <summary>Owned store preparation and bounded shutdown admission, separate from execution transactions.</summary>
public interface IStoreBootstrap
{
    /// <summary>Rejects a runtime configuration that differs from the store configuration before recovery or worker startup.</summary>
    ValueTask ValidateRuntimeOptionsAsync(RuntimeOptions options, CancellationToken cancellationToken);
    /// <summary>Validates owned state and performs conservative start recovery before workers can reserve.</summary>
    ValueTask StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken);
    /// <summary>Atomically prevents later acceptance commits in this store lifetime.</summary>
    ValueTask StopAcceptanceAsync(CancellationToken cancellationToken);
}
