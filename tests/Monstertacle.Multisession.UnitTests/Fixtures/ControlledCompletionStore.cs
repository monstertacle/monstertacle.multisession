using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Dispatch-bound observations and finalization share the same authoritative state gate.
internal abstract class ControlledCompletionStore : ControlledQueryStore
{
    public ControlledDispatchState DispatchState { get => State.Dispatch; init => State.Dispatch = value; }

    public async ValueTask CommitObservationAsync(Guid dispatchId, ProviderObservation observation, CancellationToken cancellationToken)
    {
        if (DispatchState.Observing is not null)
        {
            await DispatchState.Observing(observation, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            if (!State.Reservations.ContainsKey(dispatchId))
            {
                throw new SessionOperationException(new PublicError(ErrorCode.RevisionConflict));
            }
            DispatchState.RecordObservation(dispatchId, observation);
        }
    }
    public async ValueTask CommitCompletionAsync(Guid dispatchId, ProviderReply reply, CancellationToken cancellationToken)
    {
        if (DispatchState.Committing is not null)
        {
            await DispatchState.Committing(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (reply.Certainty is not (ExecutionCertainty.ConfirmedEnded or ExecutionCertainty.NotStarted))
        {
            throw new NotImplementedException("Controlled completion requires confirmed end or no-start evidence.");
        }
        lock (State.Gate)
        {
            if (!State.Reservations.TryGetValue(dispatchId, out ExecutionReservation? reservation)
                || State.Requests[reservation.RequestId].State != RequestState.Running
                || DispatchState.Snapshots[dispatchId].BindingRevision != State.Sessions[reservation.SessionId].BindingRevision)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.RevisionConflict));
            }

            ReservedDispatch snapshot = DispatchState.Snapshots[dispatchId];
            SessionSnapshot session = State.Sessions[reservation.SessionId];
            SessionSnapshot updated = reply.Binding is null ? session : session with
            {
                Binding = reply.Binding,
                BindingRevision = checked(session.BindingRevision + 1),
            };
            State.Requests[reservation.RequestId] = State.Requests[reservation.RequestId] with
            {
                State = reply.Outcome,
                Certainty = reply.Certainty,
                Result = reply,
            };
            DispatchState.RecordCompletion(snapshot, reply);
            State.Sessions[reservation.SessionId] = updated with
            {
                BlockReason = SessionBindingPolicy.AfterCompletion(updated.BlockReason, reply),
                UpdatedUtc = State.Clock.GetUtcNow().ToUniversalTime(),
            };
            State.Reservations.Remove(dispatchId);
        }
        DispatchState.Completed?.Invoke();

    }
    public async ValueTask MarkUncertainAsync(Guid dispatchId, PublicError failure, CancellationToken cancellationToken)
    {
        if (DispatchState.MarkingUncertain is not null)
        {
            await DispatchState.MarkingUncertain(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            ExecutionReservation reservation = State.Reservations[dispatchId];
            ControlledRequestFinalization.Unknown(State, reservation, failure);
        }
    }
}
