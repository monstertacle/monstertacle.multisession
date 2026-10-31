namespace Monstertacle.Multisession.Contracts;

/// <summary>Durable request acceptance and observation.</summary>
public interface IRequests
{
    /// <summary>Durably admits the caller-selected request identity before returning its receipt.</summary>
    ValueTask<RequestReceipt> SubmitAsync(Guid requestId, SubmitRequest request, CancellationToken cancellationToken);
    /// <summary>Reads a request belonging to this instance.</summary>
    ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken);
    /// <summary>Lists session requests, including those whose receipt was lost.</summary>
    ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Requests cancellation without claiming external termination.</summary>
    ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken);
}
