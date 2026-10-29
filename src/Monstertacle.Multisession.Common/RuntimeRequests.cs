using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession;

internal sealed class RuntimeRequests(RuntimeLifecycle lifecycle, RequestAdmission admission) : IRequests
{
    public async ValueTask<RequestReceipt> SubmitAsync(SubmitRequest request, CancellationToken cancellationToken)
    {
        if (!lifecycle.IsRunning)
        {
            throw new SessionOperationException(new PublicError(ErrorCode.SessionClosed, SessionId: request.SessionId, AdmissionStatus: AdmissionStatus.NotAccepted));
        }
        RequestReceipt receipt = await admission.SubmitAsync(request, cancellationToken);
        lifecycle.Pulse();
        return receipt;
    }
    public ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return admission.GetRequestAsync(requestId, cancellationToken);
    }
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return admission.ListRequestsAsync(sessionId, pageSize, cursor, cancellationToken);
    }
    public ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        return admission.CancelRequestAsync(requestId, cancellationToken);
    }
}
