using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;

namespace Monstertacle.Multisession;

// Frozen host composition, independent from runtime lifetime state.
internal sealed record RuntimeHost(ISessionStore Store, IStateDirectoryLease Lease,
    IReadOnlyList<ProviderRegistration> Providers, IReadOnlyList<AgentProfile> Profiles, TimeProvider Clock, SessionDiagnostics? Diagnostics);
