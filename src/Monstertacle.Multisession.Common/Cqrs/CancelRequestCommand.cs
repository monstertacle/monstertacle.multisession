using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Records cancellation intent without promising remote termination.</summary>
/// <param name="RequestId">The RequestId argument of the corresponding runtime operation.</param>
public sealed record CancelRequestCommand(Guid RequestId) : ICommand<CommandAcknowledgement>;
