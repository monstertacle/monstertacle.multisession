using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal static class SessionRoutes
{
    internal static void Register(CommandDispatcher commands, QueryDispatcher queries, ISessions port)
    {
        SessionCommandHandlers commandHandlers = new(port);
        SessionQueryHandlers queryHandlers = new(port);
        commands.Register<CreateSessionCommand, SessionSnapshot>(commandHandlers);
        commands.Register<CloseSessionCommand, CommandAcknowledgement>(commandHandlers);
        commands.Register<DeleteSessionCommand, CommandAcknowledgement>(commandHandlers);
        queries.Register<GetSessionQuery, LookupResult<SessionSnapshot>>(queryHandlers);
        queries.Register<ListSessionsQuery, Page<SessionSnapshot>>(queryHandlers);
        queries.Register<ReadHistoryQuery, Page<HistoryEntry>>(queryHandlers);
    }
}
