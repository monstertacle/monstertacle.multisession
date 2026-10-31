using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Reads an optional local request.</summary>
/// <param name="RequestId">The RequestId argument of the corresponding runtime operation.</param>
public sealed record GetRequestQuery(Guid RequestId) : IQuery<LookupResult<RequestSnapshot>>;
