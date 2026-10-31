using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>Typed native outcome facts returned as the CLR raw representation of an agent response by explicitly qualified agents.</summary>
/// <remarks>Model text or JSON never selects this contract; only this exact CLR type is recognized.</remarks>
public sealed record FrameworkExecutionOutcome
{
    /// <summary>Gets the local terminal state: succeeded, failed, cancelled or timed out.</summary>
    public required RequestState State { get; init; }
    /// <summary>Gets the evidence about external termination.</summary>
    public required ExecutionCertainty Certainty { get; init; }
    /// <summary>Gets the actually captured output, complete or partial; never fabricated.</summary>
    public string Output { get; init; } = string.Empty;
    /// <summary>Gets whether <see cref="Output"/> is the complete final output.</summary>
    public bool OutputComplete { get; init; }
    /// <summary>Gets whether the native session may continue after this turn.</summary>
    public bool ContinuationAvailable { get; init; }
    /// <summary>Gets the stable public failure category; required exactly for non-successful states.</summary>
    public ErrorCode? ErrorCode { get; init; }
}
