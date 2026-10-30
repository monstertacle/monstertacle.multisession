using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Applies an explicit revision-bound recovery decision.</summary>
/// <param name="Decision">The Decision argument of the corresponding runtime operation.</param>
public sealed record ResolveRecoveryCommand(RecoveryDecision Decision) : ICommand<CommandAcknowledgement>;
