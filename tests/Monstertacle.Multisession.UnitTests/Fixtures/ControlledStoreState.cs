using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Shared controlled state, never a persistence implementation.
internal sealed class ControlledStoreState
{
    public object Gate { get; } = new();
    public Dictionary<Guid, SessionSnapshot> Sessions { get; } = [];
    public Dictionary<Guid, RequestSnapshot> Requests { get; } = [];
    public Dictionary<Guid, SubmitRequest> Submissions { get; } = [];
    public Dictionary<Guid, DateTimeOffset> AcceptedUtc { get; } = [];
    public Dictionary<Guid, ExecutionReservation> Reservations { get; } = [];
    public ControlledDispatchState Dispatch { get; set; } = new();
    public ControlledRecoveryState Recovery { get; } = new();
    public TimeProvider Clock { get; set; } = TimeProvider.System;
}
