namespace Monstertacle.Multisession.Contracts;

/// <summary>A receipt issued only after durable acceptance.</summary>
/// <param name="RequestId">Unique local request identity.</param>
/// <param name="Sequence">Transactionally assigned order within the session.</param>
public sealed record RequestReceipt(Guid RequestId, long Sequence);
