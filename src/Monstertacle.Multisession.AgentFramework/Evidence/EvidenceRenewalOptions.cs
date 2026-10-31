namespace Monstertacle.Multisession.AgentFramework.Evidence;
/// <summary>Finite observation budget; none of these options allow a new submission.</summary>
public sealed record EvidenceRenewalOptions
{
    /// <summary>Gets the maximum observations of one associated request per round.</summary>
    public int MaximumAttemptsPerNode { get; init; } = 3;
    /// <summary>Gets the cancellable interval between observations of pending work.</summary>
    public TimeSpan ObservationInterval { get; init; } = TimeSpan.FromMilliseconds(100);
    /// <summary>Gets the finite deadline for the entire round, including validators and reads.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
