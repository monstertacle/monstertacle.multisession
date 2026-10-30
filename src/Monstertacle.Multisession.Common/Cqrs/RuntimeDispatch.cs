using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed record RuntimeDispatch(ICommandDispatcher Commands, IQueryDispatcher Queries)
{
    internal static RuntimeDispatch Create(RuntimeFacades ports, RuntimeLifecycle lifetime)
    {
        CommandDispatcher commands = new();
        QueryDispatcher queries = new();
        SessionRoutes.Register(commands, queries, ports.Sessions);
        RequestRoutes.Register(commands, queries, ports.Requests);
        RecoveryRoutes.Register(commands, queries, ports.Recovery);
        return new(new RuntimeCommandDispatch(commands, lifetime), new RuntimeQueryDispatch(queries, lifetime));
    }
}
