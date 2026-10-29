using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Persists cancellation intent without treating acknowledgement as external termination.</summary>
public sealed class RequestCancellation(IRequestCancellationStore store)
{
    /// <summary>Returns after the local cancellation transaction; running termination remains separately observable.</summary>
    public async ValueTask CancelAsync(Guid requestId, CancellationToken cancellationToken)
        => await store.RequestCancellationAsync(requestId, cancellationToken);
}
