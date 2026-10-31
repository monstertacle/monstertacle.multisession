using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Separates durable cancellation intent from evidence that an execution ended.</summary>
public static class RequestCancellationPolicy
{
    /// <summary>Finalizes only queued input; a running request keeps its evidence and reservation.</summary>
    public static RequestSnapshot Apply(RequestSnapshot request, DateTimeOffset requestedUtc)
    {
        if (!RequestStateMachine.IsValid(request.State, request.Certainty)) { throw new SessionOperationException(new(ErrorCode.StorageUnavailable)); }
        if (RequestStateMachine.IsTerminal(request.State) || request.CancellationRequestedUtc is not null) { return request; }
        DateTimeOffset now = requestedUtc.ToUniversalTime();
        if (request.State == RequestState.Running) { return request with { CancellationRequestedUtc = now }; }
        return request with
        {
            State = RequestState.Cancelled,
            Certainty = ExecutionCertainty.NotStarted,
            CancellationRequestedUtc = now,
            EndedUtc = now,
            Result = new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null,
                new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, request.SessionId, request.RequestId)),
        };
    }
}
