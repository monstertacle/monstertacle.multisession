using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RecoveryQueryHandlers(IRecovery port) :
    IQueryHandler<GetRecoveryCaseQuery, LookupResult<RecoveryCaseSnapshot>>,
    IQueryHandler<ListRecoveryCasesQuery, Page<RecoveryCaseSnapshot>>,
    IQueryHandler<InspectRecoveryQuery, ProviderReply>
{
    public async Task<LookupResult<RecoveryCaseSnapshot>> HandleAsync(GetRecoveryCaseQuery request, CancellationToken token)
        => new(await port.GetRecoveryCaseAsync(request.Target, token));
    public Task<Page<RecoveryCaseSnapshot>> HandleAsync(ListRecoveryCasesQuery request, CancellationToken token)
        => port.ListRecoveryCasesAsync(request.PageSize, request.Cursor, token).AsTask();
    public Task<ProviderReply> HandleAsync(InspectRecoveryQuery request, CancellationToken token)
        => port.InspectRecoveryAsync(request.Target, token).AsTask();
}
