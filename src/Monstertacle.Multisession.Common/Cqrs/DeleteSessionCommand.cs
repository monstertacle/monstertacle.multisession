using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Deletes a closed, conflict-free local session.</summary>
/// <param name="SessionId">The SessionId argument of the corresponding runtime operation.</param>
public sealed record DeleteSessionCommand(Guid SessionId) : ICommand<CommandAcknowledgement>;
