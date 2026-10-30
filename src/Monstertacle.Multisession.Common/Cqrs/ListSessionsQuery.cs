using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Lists sessions in bounded stable order.</summary>
/// <param name="PageSize">The PageSize argument of the corresponding runtime operation.</param>
/// <param name="Cursor">The Cursor argument of the corresponding runtime operation.</param>
public sealed record ListSessionsQuery(int PageSize, string? Cursor = null) : IQuery<Page<SessionSnapshot>>;
