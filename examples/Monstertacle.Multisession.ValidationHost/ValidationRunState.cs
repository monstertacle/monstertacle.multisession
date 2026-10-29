using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ValidationRunState(Guid runId, IStateDirectoryLease lease) : IAsyncDisposable
{
    internal Guid RunId { get; } = runId;
    internal Guid TrustScope { get; } = Guid.NewGuid();
    internal IStateDirectoryLease Lease { get; } = lease;
    internal string Directory => Lease.StateDirectory;
    public ValueTask DisposeAsync() => Lease.DisposeAsync();
}
