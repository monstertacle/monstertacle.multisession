using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Queries;

/// <summary>A complete stable key, interpreted according to its cursor list kind.</summary>
/// <param name="Id">Last local identity.</param>
/// <param name="Ordinal">Accepted sequence or history ordinal.</param>
/// <param name="Utc">UTC creation time for sessions/recovery.</param>
/// <param name="TargetKind">Explicit recovery target ordering.</param>
public sealed record CursorKey(Guid Id, long? Ordinal = null, DateTimeOffset? Utc = null, RecoveryTargetKind? TargetKind = null);
