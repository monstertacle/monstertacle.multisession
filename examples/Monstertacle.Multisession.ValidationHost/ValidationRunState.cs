using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ValidationRunState(Guid runId, IStateDirectoryLease lease, Guid? trustScope = null) : IAsyncDisposable
{
    internal Guid RunId { get; } = runId;
    internal Guid TrustScope { get; } = trustScope ?? Guid.NewGuid();
    internal IStateDirectoryLease Lease { get; } = lease;
    internal string Directory => Lease.StateDirectory;
    public ValueTask DisposeAsync() => Lease.DisposeAsync();
}
