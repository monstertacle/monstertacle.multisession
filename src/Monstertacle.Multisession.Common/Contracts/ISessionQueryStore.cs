namespace Monstertacle.Multisession.Contracts;

/// <summary>Bounded domain reads, separated from the atomic write contract.</summary>
public interface ISessionQueryStore
{
    /// <summary>Reads a session owned by this store.</summary>
    ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    /// <summary>Lists sessions in stable bounded order.</summary>
    ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Reads locally observed history in stable bounded order.</summary>
    ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Reads an accepted request.</summary>
    ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken);
    /// <summary>Lists accepted session requests, including those whose receipt was lost.</summary>
    ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Lists requests that still require explicit recovery.</summary>
    ValueTask<Page<RequestSnapshot>> ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken);
}
