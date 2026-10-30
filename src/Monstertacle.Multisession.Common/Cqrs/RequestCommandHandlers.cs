using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RequestCommandHandlers(IRequests port) :
    ICommandHandler<SubmitRequestCommand, RequestReceipt>,
    ICommandHandler<CancelRequestCommand, CommandAcknowledgement>
{
    public Task<RequestReceipt> HandleAsync(SubmitRequestCommand request, CancellationToken token)
        => port.SubmitAsync(request.Request, token).AsTask();
    public async Task<CommandAcknowledgement> HandleAsync(CancelRequestCommand request, CancellationToken token)
    {
        await port.CancelRequestAsync(request.RequestId, token);
        return new();
    }
}
