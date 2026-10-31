using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal static class RecoveryRoutes
{
    internal static void Register(CommandDispatcher commands, QueryDispatcher queries, IRecovery port)
    {
        RecoveryCommandHandlers commandHandlers = new(port);
        RecoveryQueryHandlers queryHandlers = new(port);
        commands.Register<ResolveRecoveryCommand, CommandAcknowledgement>(commandHandlers);
        queries.Register<GetRecoveryCaseQuery, LookupResult<RecoveryCaseSnapshot>>(queryHandlers);
        queries.Register<ListRecoveryCasesQuery, Page<RecoveryCaseSnapshot>>(queryHandlers);
        queries.Register<InspectRecoveryQuery, ProviderReply>(queryHandlers);
    }
}
