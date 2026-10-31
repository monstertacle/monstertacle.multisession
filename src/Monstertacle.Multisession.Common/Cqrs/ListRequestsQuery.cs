using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Lists requests belonging to a session.</summary>
/// <param name="SessionId">The SessionId argument of the corresponding runtime operation.</param>
/// <param name="PageSize">The PageSize argument of the corresponding runtime operation.</param>
/// <param name="Cursor">The Cursor argument of the corresponding runtime operation.</param>
public sealed record ListRequestsQuery(Guid SessionId, int PageSize, string? Cursor = null) : IQuery<Page<RequestSnapshot>>;
