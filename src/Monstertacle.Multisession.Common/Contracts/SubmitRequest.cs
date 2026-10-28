namespace Monstertacle.Multisession.Contracts;

/// <summary>A new host submission; timeout starts on dispatch, not in the durable queue.</summary>
/// <param name="SessionId">Target local session.</param>
/// <param name="Input">Only the new unmodified message.</param>
/// <param name="Timeout">Finite execution timeout.</param>
/// <param name="WorkspaceKey">Canonical host serialization key.</param>
/// <param name="WriteIntent">Whether a workspace key is required to serialize writes.</param>
public sealed record SubmitRequest(Guid SessionId, string Input, TimeSpan Timeout, string? WorkspaceKey, bool WriteIntent);
