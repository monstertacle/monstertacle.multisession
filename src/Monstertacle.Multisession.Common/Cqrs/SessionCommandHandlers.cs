using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class SessionCommandHandlers(ISessions port) :
    ICommandHandler<CreateSessionCommand, SessionSnapshot>,
    ICommandHandler<CloseSessionCommand, CommandAcknowledgement>,
    ICommandHandler<DeleteSessionCommand, CommandAcknowledgement>
{
    public Task<SessionSnapshot> HandleAsync(CreateSessionCommand request, CancellationToken token)
        => port.CreateSessionAsync(request.SessionId, request.AgentProfileId, request.Context, token).AsTask();
    public async Task<CommandAcknowledgement> HandleAsync(CloseSessionCommand request, CancellationToken token)
    {
        await port.CloseSessionAsync(request.SessionId, token);
        return new();
    }
    public async Task<CommandAcknowledgement> HandleAsync(DeleteSessionCommand request, CancellationToken token)
    {
        await port.DeleteSessionAsync(request.SessionId, token);
        return new();
    }
}
