using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Read-only session/history operations independent from native lifecycle I/O.</summary>
public sealed class SessionQueries(ISessionQueryStore store)
{
    /// <summary>Reads a local immutable session snapshot.</summary>
    public ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => store.GetSessionAsync(sessionId, cancellationToken);
    /// <summary>Lists bounded session snapshots, using the store's cursor authority.</summary>
    public ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        PageCursor.ValidateSize(pageSize);
        return store.ListSessionsAsync(pageSize, cursor, cancellationToken);
    }
    /// <summary>Reads locally observed history in stable keyset order.</summary>
    public ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        PageCursor.ValidateSize(pageSize);
        return store.ReadHistoryAsync(sessionId, pageSize, cursor, cancellationToken);
    }
}
