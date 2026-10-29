namespace Monstertacle.Multisession.Contracts;

/// <summary>Owned store preparation and bounded shutdown admission, separate from execution transactions.</summary>
public interface IStoreBootstrap
{
    /// <summary>Validates owned state and performs conservative start recovery before workers can reserve.</summary>
    ValueTask StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken);
    /// <summary>Atomically prevents later acceptance commits in this store lifetime.</summary>
    ValueTask StopAcceptanceAsync(CancellationToken cancellationToken);
}
