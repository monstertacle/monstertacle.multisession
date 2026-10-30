using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession;

// Composes prepared ports; lifecycle state and ownership remain with the caller.
internal static class RuntimePreparation
{
    internal static async Task<RuntimeComponents> PrepareAsync(RuntimeHost host, RuntimeLifecycle lifetime, RuntimeOptions options, CancellationToken token)
    {
        SessionCatalog catalog = new(host.Store, host.Providers, host.Profiles, host.Clock);
        await new RecoveryCoordinator(host.Store, host.Providers, options, host.Clock).PrepareAsync(token);
        RequestAdmission admission = new(host.Store, options, host.Clock);
        RecoveryService recovery = new(host.Store, host.Providers, options);
        RuntimeFacades ports = new(new RuntimeSessions(lifetime, catalog), new RuntimeRequests(lifetime, admission, host.Diagnostics),
            new RuntimeRecovery(lifetime, recovery));
        RuntimeDispatch dispatch = RuntimeDispatch.Create(ports, lifetime);
        RuntimeFacades facades = new(new CqrsSessions(dispatch), new CqrsRequests(dispatch), new CqrsRecovery(dispatch));
        return new(facades, dispatch, new(host.Store, host.Providers, options, host.Clock, host.Diagnostics), options,
            new(host.Store, host.Diagnostics));
    }
}
