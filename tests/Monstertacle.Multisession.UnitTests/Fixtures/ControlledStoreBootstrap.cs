using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled restart projection, not a crash/SQLite/native ownership proof.
internal static class ControlledStoreBootstrap
{
    public static void Recover(ControlledStoreState state)
    {
        RequestSnapshot[] running = state.Requests.Values.Where(item => item.State == RequestState.Running).ToArray();
        if (running.Any(request => !state.Reservations.Values.Any(reservation => reservation.RequestId == request.RequestId)))
        {
            throw new SessionOperationException(new PublicError(ErrorCode.StorageUnavailable));
        }
        foreach (SessionSnapshot session in state.Sessions.Values.Where(item => item.Lifecycle == SessionLifecycle.Opening).ToArray())
        {
            state.Sessions[session.SessionId] = session with { BlockReason = SessionBlockReason.RecoveryRequired, UpdatedUtc = state.Clock.GetUtcNow().ToUniversalTime() };
            RecoveryTarget target = new(RecoveryTargetKind.SessionOpening, session.SessionId);
            if (state.Recovery.Read(target)?.Status != RecoveryCaseStatus.Open)
            {
                state.Recovery.Open(state.Sessions[session.SessionId], null, state.Clock.GetUtcNow(), null);
            }
        }
        foreach (RequestSnapshot request in running)
        {
            ExecutionReservation reservation = state.Reservations.Values.Single(item => item.RequestId == request.RequestId);
            ControlledRequestFinalization.Unknown(state, reservation, new(ErrorCode.Interrupted, ExecutionCertainty.Unknown, request.SessionId, request.RequestId));
        }
    }
}
