namespace Monstertacle.Multisession.Contracts;

/// <summary>Observed local history, never input for implicit transcript replay.</summary>
/// <param name="SessionId">Owning session.</param>
/// <param name="RequestId">Accepted request.</param>
/// <param name="Sequence">Request order.</param>
/// <param name="Input">Observed input.</param>
/// <param name="Result">Recorded final result.</param>
public sealed record HistoryEntry(Guid SessionId, Guid RequestId, long Sequence, string Input, ProviderReply Result);
