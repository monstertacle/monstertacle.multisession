using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RequestQueryHandlers(IRequests port) :
    IQueryHandler<GetRequestQuery, LookupResult<RequestSnapshot>>,
    IQueryHandler<ListRequestsQuery, Page<RequestSnapshot>>
{
    public async Task<LookupResult<RequestSnapshot>> HandleAsync(GetRequestQuery request, CancellationToken token)
        => new(await port.GetRequestAsync(request.RequestId, token));
    public Task<Page<RequestSnapshot>> HandleAsync(ListRequestsQuery request, CancellationToken token)
        => port.ListRequestsAsync(request.SessionId, request.PageSize, request.Cursor, token).AsTask();
}
