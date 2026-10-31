using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class CqrsRequests(RuntimeDispatch dispatch) : IRequests
{
    public ValueTask<RequestReceipt> SubmitAsync(Guid requestId, SubmitRequest request, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new SubmitRequestCommand(requestId, request), cancellationToken));
    public ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new CancelRequestCommand(requestId), cancellationToken));
    public async ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => (await dispatch.Queries.DispatchAsync(new GetRequestQuery(requestId), cancellationToken)).Value;
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(dispatch.Queries.DispatchAsync(new ListRequestsQuery(sessionId, pageSize, cursor), cancellationToken));
}
