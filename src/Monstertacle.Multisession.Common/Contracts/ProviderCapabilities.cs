namespace Monstertacle.Multisession.Contracts;

/// <summary>Explicit adapter abilities, never inferred from its product name.</summary>
/// <param name="SupportsModelResponses">Can execute model-response requests.</param>
/// <param name="SupportsAgentTasks">Can execute external agent tasks.</param>
/// <param name="SupportsInspection">Can inspect native operations.</param>
/// <param name="SupportsCancellation">Can request native cancellation.</param>
/// <param name="RequiresWorkspace">Requires an absolute workspace directory.</param>
public sealed record ProviderCapabilities(
    bool SupportsModelResponses,
    bool SupportsAgentTasks,
    bool SupportsInspection,
    bool SupportsCancellation,
    bool RequiresWorkspace)
{
    /// <summary>Gets whether recovery must verify the uncertain execution before publishing a continuation.</summary>
    public bool RequiresOperationAwareRecovery { get; init; }
}
