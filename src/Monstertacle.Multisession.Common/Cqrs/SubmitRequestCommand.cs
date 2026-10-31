using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Accepts a request durably before returning its receipt.</summary>
/// <param name="RequestId">The identity persisted by the caller before invocation.</param>
/// <param name="Request">The Request argument of the corresponding runtime operation.</param>
public sealed record SubmitRequestCommand(Guid RequestId, SubmitRequest Request) : ICommand<RequestReceipt>;
