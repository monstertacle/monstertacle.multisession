using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RecoveryCommandHandlers(IRecovery port) :
    ICommandHandler<ResolveRecoveryCommand, CommandAcknowledgement>
{
    public async Task<CommandAcknowledgement> HandleAsync(ResolveRecoveryCommand request, CancellationToken token)
    {
        await port.ResolveRecoveryAsync(request.Decision, token);
        return new();
    }
}
