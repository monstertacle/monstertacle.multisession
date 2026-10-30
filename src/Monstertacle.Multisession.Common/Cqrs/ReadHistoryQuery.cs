using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Reads locally observed session history.</summary>
/// <param name="SessionId">The SessionId argument of the corresponding runtime operation.</param>
/// <param name="PageSize">The PageSize argument of the corresponding runtime operation.</param>
/// <param name="Cursor">The Cursor argument of the corresponding runtime operation.</param>
public sealed record ReadHistoryQuery(Guid SessionId, int PageSize, string? Cursor = null) : IQuery<Page<HistoryEntry>>;
