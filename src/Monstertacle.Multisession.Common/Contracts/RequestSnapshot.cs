namespace Monstertacle.Multisession.Contracts;

/// <summary>Public local state without an assumption that a local end stopped external work.</summary>
/// <param name="RequestId">Local accepted request.</param>
/// <param name="SessionId">Owning session.</param>
/// <param name="Sequence">Durable order within that session.</param>
/// <param name="State">Local lifecycle state.</param>
/// <param name="Certainty">Separately recorded execution evidence.</param>
/// <param name="Result">Final result when locally available.</param>
public sealed record RequestSnapshot(
    Guid RequestId,
    Guid SessionId,
    long Sequence,
    RequestState State,
    ExecutionCertainty Certainty,
    ProviderReply? Result)
{
    /// <summary>Gets the UTC instant at which the input was durably accepted.</summary>
    public DateTimeOffset AcceptedUtc { get; init; }
    /// <summary>Gets the actual dispatch time, absent while queued.</summary>
    public DateTimeOffset? StartedUtc { get; init; }
    /// <summary>Gets the local terminal time, independent from external end certainty.</summary>
    public DateTimeOffset? EndedUtc { get; init; }
    /// <summary>Gets separately recorded cancellation intent, which does not prove external end.</summary>
    public DateTimeOffset? CancellationRequestedUtc { get; init; }
}
