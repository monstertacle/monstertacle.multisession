using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;

namespace Monstertacle.Multisession;

internal sealed class RuntimeRecovery(RuntimeLifecycle lifecycle, RecoveryService recovery) : IRecovery
{
    public ValueTask<RecoveryCaseSnapshot?> GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return recovery.GetRecoveryCaseAsync(target, cancellationToken);
    }
    public ValueTask<Page<RecoveryCaseSnapshot>> ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return recovery.ListRecoveryCasesAsync(pageSize, cursor, cancellationToken);
    }
    public ValueTask<ProviderReply> InspectRecoveryAsync(RecoveryTarget target, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        return recovery.InspectRecoveryAsync(target, cancellationToken);
    }
    public async ValueTask ResolveRecoveryAsync(RecoveryDecision decision, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        await recovery.ResolveRecoveryAsync(decision, cancellationToken);
        lifecycle.Pulse();
    }
}
