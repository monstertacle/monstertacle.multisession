namespace Monstertacle.Multisession.Queries;

/// <summary>A cursor scope; it is not a permission or authorization token.</summary>
/// <param name="StoreId">Authoritative store identity.</param>
/// <param name="Kind">List kind.</param>
/// <param name="SessionId">Session for request/history lists only.</param>
public sealed record PageScope(Guid StoreId, PageListKind Kind, Guid? SessionId = null);
