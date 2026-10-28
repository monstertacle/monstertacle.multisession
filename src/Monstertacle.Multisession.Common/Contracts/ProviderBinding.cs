namespace Monstertacle.Multisession.Contracts;

/// <summary>Opaque stable identity and separately advancing confirmed continuation.</summary>
/// <param name="AdapterId">Registered adapter identity.</param>
/// <param name="FormatVersion">Explicit adapter-owned binding format.</param>
/// <param name="SessionReference">Stable native session identity; null only for safe lazy opening.</param>
/// <param name="ContinuationState">Last confirmed continuation, not a replacement session identity.</param>
public sealed record ProviderBinding(
    string AdapterId,
    int FormatVersion,
    string? SessionReference,
    string? ContinuationState);
