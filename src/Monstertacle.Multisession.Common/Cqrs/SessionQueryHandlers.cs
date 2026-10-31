using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class SessionQueryHandlers(ISessions port) :
    IQueryHandler<GetSessionQuery, LookupResult<SessionSnapshot>>,
    IQueryHandler<ListSessionsQuery, Page<SessionSnapshot>>,
    IQueryHandler<ReadHistoryQuery, Page<HistoryEntry>>
{
    public async Task<LookupResult<SessionSnapshot>> HandleAsync(GetSessionQuery request, CancellationToken token)
        => new(await port.GetSessionAsync(request.SessionId, token));
    public Task<Page<SessionSnapshot>> HandleAsync(ListSessionsQuery request, CancellationToken token)
        => port.ListSessionsAsync(request.PageSize, request.Cursor, token).AsTask();
    public Task<Page<HistoryEntry>> HandleAsync(ReadHistoryQuery request, CancellationToken token)
        => port.ReadHistoryAsync(request.SessionId, request.PageSize, request.Cursor, token).AsTask();
}
