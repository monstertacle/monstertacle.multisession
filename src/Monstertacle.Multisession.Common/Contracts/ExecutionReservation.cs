namespace Monstertacle.Multisession.Contracts;

/// <summary>A durable dispatch reservation which survives uncertain local completion.</summary>
/// <param name="DispatchId">Unique reserved dispatch.</param>
/// <param name="RequestId">Accepted request.</param>
/// <param name="SessionId">Reserved session.</param>
/// <param name="WorkspaceKey">Reserved write workspace, when applicable.</param>
/// <param name="ReservedUtc">UTC reservation time before external execution.</param>
public sealed record ExecutionReservation(Guid DispatchId, Guid RequestId, Guid SessionId, string? WorkspaceKey, DateTimeOffset ReservedUtc);
