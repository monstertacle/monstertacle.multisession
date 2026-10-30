using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Reads an optional local session.</summary>
/// <param name="SessionId">The SessionId argument of the corresponding runtime operation.</param>
public sealed record GetSessionQuery(Guid SessionId) : IQuery<LookupResult<SessionSnapshot>>;
