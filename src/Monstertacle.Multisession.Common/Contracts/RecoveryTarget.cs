namespace Monstertacle.Multisession.Contracts;

/// <summary>An explicit recovery identity without sentinel or artificial requests.</summary>
/// <param name="Kind">Opening or execution.</param>
/// <param name="Id">Local session ID for opening, request ID for execution.</param>
public sealed record RecoveryTarget(RecoveryTargetKind Kind, Guid Id);
