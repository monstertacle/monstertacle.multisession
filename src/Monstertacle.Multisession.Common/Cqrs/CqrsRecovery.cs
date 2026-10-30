using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class CqrsRecovery(RuntimeDispatch dispatch) : IRecovery
{
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new ResolveRecoveryCommand(decision), cancellationToken));
    public async ValueTask<RecoveryCaseSnapshot?> GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
        => (await dispatch.Queries.DispatchAsync(new GetRecoveryCaseQuery(target), cancellationToken)).Value;
    public ValueTask<Page<RecoveryCaseSnapshot>> ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(dispatch.Queries.DispatchAsync(new ListRecoveryCasesQuery(pageSize, cursor), cancellationToken));
    public ValueTask<ProviderReply> InspectRecoveryAsync(RecoveryTarget target, CancellationToken cancellationToken)
        => new(dispatch.Queries.DispatchAsync(new InspectRecoveryQuery(target), cancellationToken));
}
