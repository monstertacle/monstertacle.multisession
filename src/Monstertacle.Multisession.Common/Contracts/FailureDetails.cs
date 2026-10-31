namespace Monstertacle.Multisession.Contracts;

/// <summary>Safe diagnostic data with no native text, SQL, paths or credentials.</summary>
/// <param name="Stage">The operation phase.</param>
/// <param name="NativeCode">Optional primary database error code.</param>
/// <param name="ExtendedNativeCode">Optional extended database error code.</param>
/// <param name="PlatformCode">Optional numeric operating-system error code.</param>
public sealed record FailureDetails(FailureStage Stage, int? NativeCode = null, int? ExtendedNativeCode = null, int? PlatformCode = null);
