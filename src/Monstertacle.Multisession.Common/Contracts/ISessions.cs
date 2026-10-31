namespace Monstertacle.Multisession.Contracts;

/// <summary>The host-neutral public session catalog.</summary>
public interface ISessions
{
    /// <summary>Creates the caller-selected session identity before establishing external state.</summary>
    ValueTask<SessionSnapshot> CreateSessionAsync(Guid sessionId, string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken);
    /// <summary>Reads a local session, or null if it does not belong to this instance.</summary>
    ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    /// <summary>Lists only this instance's sessions in bounded stable order.</summary>
    ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Reads locally observed history without claiming complete provider history.</summary>
    ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Closes acceptance without silently releasing uncertain external work.</summary>
    ValueTask CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    /// <summary>Deletes only a closed, conflict-free local session.</summary>
    ValueTask DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
