using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal static class RequestRoutes
{
    internal static void Register(CommandDispatcher commands, QueryDispatcher queries, IRequests port)
    {
        RequestCommandHandlers commandHandlers = new(port);
        RequestQueryHandlers queryHandlers = new(port);
        commands.Register<SubmitRequestCommand, RequestReceipt>(commandHandlers);
        commands.Register<CancelRequestCommand, CommandAcknowledgement>(commandHandlers);
        queries.Register<GetRequestQuery, LookupResult<RequestSnapshot>>(queryHandlers);
        queries.Register<ListRequestsQuery, Page<RequestSnapshot>>(queryHandlers);
    }
}
