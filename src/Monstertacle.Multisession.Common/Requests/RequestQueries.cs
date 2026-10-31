using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.Requests;

/// <summary>Read-only request lookup and bounded continuation within one session.</summary>
public sealed class RequestQueries(ISessionQueryStore store)
{
    /// <summary>Reads an accepted request, including after a lost receipt.</summary>
    public ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => store.GetRequestAsync(requestId, cancellationToken);
    /// <summary>Lists requests using the store's authoritative scope and keyset.</summary>
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        PageCursor.ValidateSize(pageSize);
        return store.ListRequestsAsync(sessionId, pageSize, cursor, cancellationToken);
    }
}
