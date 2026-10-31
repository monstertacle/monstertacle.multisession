using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Creates a local session using the configured profile.</summary>
/// <param name="SessionId">The identity persisted by the caller before invocation.</param>
/// <param name="AgentProfileId">The AgentProfileId argument of the corresponding runtime operation.</param>
/// <param name="Context">The Context argument of the corresponding runtime operation.</param>
public sealed record CreateSessionCommand(Guid SessionId, string AgentProfileId, SessionExecutionContext Context) : ICommand<SessionSnapshot>;
