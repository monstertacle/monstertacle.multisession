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
    ProviderReply? Result);
