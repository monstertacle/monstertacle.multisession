using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession;

internal sealed class RuntimeSessions(RuntimeLifecycle lifecycle, SessionCatalog catalog) : ISessions
{
    public ValueTask<SessionSnapshot> CreateSessionAsync(string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        return catalog.CreateSessionAsync(agentProfileId, context, cancellationToken);
    }
    public ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return catalog.GetSessionAsync(sessionId, cancellationToken);
    }
    public ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return catalog.ListSessionsAsync(pageSize, cursor, cancellationToken);
    }
    public ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        lifecycle.RequireReadable();
        return catalog.ReadHistoryAsync(sessionId, pageSize, cursor, cancellationToken);
    }
    public ValueTask CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        return catalog.CloseSessionAsync(sessionId, cancellationToken);
    }
    public ValueTask DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        lifecycle.RequireRunning();
        return catalog.DeleteSessionAsync(sessionId, cancellationToken);
    }
}
