namespace Monstertacle.Multisession.Contracts;

/// <summary>A consistent local snapshot; unknown reservations remain included in occupied reservations.</summary>
/// <param name="QueuedRequests">Requests waiting for reservation.</param>
/// <param name="OccupiedReservations">All unreleased reservations, including unknown executions.</param>
/// <param name="UncertainReservations">Unreleased reservations whose request has unknown external certainty.</param>
public sealed record RuntimeStatistics(long QueuedRequests, long OccupiedReservations, long UncertainReservations);
