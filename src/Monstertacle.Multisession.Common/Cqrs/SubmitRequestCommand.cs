using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Accepts a request durably before returning its receipt.</summary>
/// <param name="Request">The Request argument of the corresponding runtime operation.</param>
public sealed record SubmitRequestCommand(SubmitRequest Request) : ICommand<RequestReceipt>;
