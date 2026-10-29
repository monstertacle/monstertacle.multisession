using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Shared controlled finalization; caller holds the common store gate.
internal static class ControlledRequestFinalization
{
    public static void Unknown(ControlledStoreState state, ExecutionReservation reservation, PublicError failure)
    {
        state.Requests[reservation.RequestId] = state.Requests[reservation.RequestId] with
        {
            State = RequestState.Interrupted,
            Certainty = ExecutionCertainty.Unknown,
            Result = new(RequestState.Interrupted, ExecutionCertainty.Unknown, string.Empty, false, null,
                failure with { Certainty = ExecutionCertainty.Unknown }),
        };
        state.Sessions[reservation.SessionId] = state.Sessions[reservation.SessionId] with { BlockReason = SessionBlockReason.RecoveryRequired };
        state.Recovery.Open(state.Sessions[reservation.SessionId], reservation, state.Clock.GetUtcNow(), state.Dispatch.OperationReference(reservation.DispatchId));
    }
}
