using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Closes local acceptance; acknowledgement does not confirm external termination.</summary>
/// <param name="SessionId">The SessionId argument of the corresponding runtime operation.</param>
public sealed record CloseSessionCommand(Guid SessionId) : ICommand<CommandAcknowledgement>;
