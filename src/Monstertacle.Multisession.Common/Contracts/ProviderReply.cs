namespace Monstertacle.Multisession.Contracts;

/// <summary>A final local result; intermediate events cannot substitute for this result.</summary>
/// <param name="Outcome">Local terminal outcome.</param>
/// <param name="Certainty">Evidence of actual external termination.</param>
/// <param name="Output">Complete final text, or empty for an output-limit failure.</param>
/// <param name="OutputComplete">Whether the complete output was captured.</param>
/// <param name="Binding">Updated confirmed native continuation, when available.</param>
/// <param name="Error">Optional stable, sanitized public error.</param>
public sealed record ProviderReply(
    RequestState Outcome,
    ExecutionCertainty Certainty,
    string Output,
    bool OutputComplete,
    ProviderBinding? Binding,
    PublicError? Error)
{
    /// <summary>Gets explicit continuation evidence; false blocks later turns, null leaves the binding policy unchanged, and true does not prove external termination.</summary>
    public bool? ContinuationAvailable { get; init; }
}
