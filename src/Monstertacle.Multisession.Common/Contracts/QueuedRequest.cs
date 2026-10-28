namespace Monstertacle.Multisession.Contracts;

/// <summary>Scheduling projection of an already validated, accepted queued request.</summary>
/// <param name="RequestId">Accepted local identity.</param>
/// <param name="SessionId">Owning session.</param>
/// <param name="Sequence">Committed per-session order.</param>
/// <param name="AcceptedUtc">Committed acceptance time.</param>
/// <param name="WriteWorkspaceKey">Canonical write key; null for a request without write intent.</param>
public sealed record QueuedRequest(Guid RequestId, Guid SessionId, long Sequence, DateTimeOffset AcceptedUtc, string? WriteWorkspaceKey);
