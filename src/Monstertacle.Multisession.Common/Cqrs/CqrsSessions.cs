using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class CqrsSessions(RuntimeDispatch dispatch) : ISessions
{
    public ValueTask<SessionSnapshot> CreateSessionAsync(Guid sessionId, string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new CreateSessionCommand(sessionId, agentProfileId, context), cancellationToken));
    public ValueTask CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new CloseSessionCommand(sessionId), cancellationToken));
    public ValueTask DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => new(dispatch.Commands.DispatchAsync(new DeleteSessionCommand(sessionId), cancellationToken));
    public async ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => (await dispatch.Queries.DispatchAsync(new GetSessionQuery(sessionId), cancellationToken)).Value;
    public ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(dispatch.Queries.DispatchAsync(new ListSessionsQuery(pageSize, cursor), cancellationToken));
    public ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(dispatch.Queries.DispatchAsync(new ReadHistoryQuery(sessionId, pageSize, cursor), cancellationToken));
}
