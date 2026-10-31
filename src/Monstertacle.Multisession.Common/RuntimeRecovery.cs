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
        return lifecycle.RunOperation(token => recovery.InspectRecoveryAsync(target, token), cancellationToken);
    }
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, CancellationToken cancellationToken)
    {
        ValueTask<bool> operation = lifecycle.RunOperation(async token =>
        {
            await recovery.ResolveRecoveryAsync(decision, token);
            lifecycle.Pulse();
            return true;
        }, cancellationToken);
        return new(operation.AsTask());
    }
}
