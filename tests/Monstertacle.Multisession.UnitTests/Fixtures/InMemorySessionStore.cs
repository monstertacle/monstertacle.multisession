using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Scheduling;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled test state only. In-memory admission does not establish SQLite or crash durability.
internal sealed class InMemorySessionStore : ControlledQueryStore, ISessionStore
{
    private readonly ControlledStoreState _state = new();
    protected override ControlledStoreState State => _state;
    public ControlledStoreState Data => State;
    public ControlledRecoveryState RecoveryState => State.Recovery;
    public ControlledDispatchState DispatchState { get => State.Dispatch; init => State.Dispatch = value; }
    public Action? CommittingBinding { get; init; }
    public Action? BlockingSession { get; init; }
    public Func<SubmitRequest, CancellationToken, ValueTask>? CommittingAcceptance { get; init; }
    public Action<RequestReceipt>? Accepted { get; init; }
    public IReadOnlyDictionary<Guid, SubmitRequest> Submissions => State.Submissions;
    public IReadOnlyDictionary<Guid, ExecutionReservation> Reservations => State.Reservations;
    public TimeProvider Clock { get => State.Clock; init => State.Clock = value; }
    public Func<CancellationToken, ValueTask>? Reserving { get; init; }

    public SessionSnapshot? FindSession(Guid sessionId) => State.Sessions.GetValueOrDefault(sessionId);

    public ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State.Sessions.Add(session.SessionId, session);
        return ValueTask.CompletedTask;
    }

    public ValueTask CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommittingBinding?.Invoke();
        lock (State.Gate)
        {
            SessionSnapshot session = State.Sessions[sessionId];
            State.Sessions[sessionId] = session with
            {
                Binding = binding,
                Lifecycle = SessionLifecycle.Open,
                BindingRevision = session.Binding is null ? 0 : checked(session.BindingRevision + 1),
            };
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BlockingSession?.Invoke();
        lock (State.Gate)
        {
            SessionSnapshot session = State.Sessions[sessionId] with { BlockReason = reason };
            State.Sessions[sessionId] = session;
            if (reason == SessionBlockReason.RecoveryRequired && session.Lifecycle == SessionLifecycle.Opening)
            {
                RecoveryState.Open(session, null, Clock.GetUtcNow(), null);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<RequestReceipt> CommitAcceptanceAsync(SubmitRequest request, int maxQueuedRequests, CancellationToken cancellationToken)
        => AcceptAsync(request, maxQueuedRequests, cancellationToken);

    private async ValueTask<RequestReceipt> AcceptAsync(SubmitRequest request, int maxQueuedRequests, CancellationToken cancellationToken)
    {
        if (CommittingAcceptance is not null)
        {
            await CommittingAcceptance(request, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RequestReceipt receipt;
        lock (State.Gate)
        {
            if (!State.Sessions.TryGetValue(request.SessionId, out SessionSnapshot? session))
            {
                throw new SessionOperationException(new PublicError(ErrorCode.NotFound, SessionId: request.SessionId));
            }

            if (session.Lifecycle != SessionLifecycle.Open || session.BlockReason != SessionBlockReason.None)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.SessionBlocked, SessionId: request.SessionId));
            }

            if (State.Requests.Values.Count(item => item.State == RequestState.Queued) >= maxQueuedRequests)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.QueueFull, SessionId: request.SessionId));
            }

            long sequence = State.Requests.Values.Where(item => item.SessionId == request.SessionId)
                .Select(item => item.Sequence).DefaultIfEmpty().Max() + 1;
            Guid requestId = Guid.NewGuid();
            State.Requests.Add(requestId, new RequestSnapshot(requestId, request.SessionId, sequence, RequestState.Queued, ExecutionCertainty.NotStarted, null));
            State.Submissions.Add(requestId, request);
            State.AcceptedUtc.Add(requestId, Clock.GetUtcNow());
            receipt = new RequestReceipt(requestId, sequence);
        }

        Accepted?.Invoke(receipt);
        return receipt;
    }
    public async ValueTask<ExecutionReservation?> TryReserveHeadAsync(int maxConcurrentRequests, CancellationToken cancellationToken)
    {
        if (Reserving is not null)
        {
            await Reserving(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            QueuedRequest[] queued = State.Requests.Values.Where(request => request.State == RequestState.Queued)
                .Select(request => new QueuedRequest(request.RequestId, request.SessionId, request.Sequence,
                    State.AcceptedUtc[request.RequestId], State.Submissions[request.RequestId].WriteIntent ? State.Submissions[request.RequestId].WorkspaceKey : null))
                .ToArray();
            QueuedRequest? head = ReservationPolicy.SelectHead(queued, State.Reservations.Values.ToArray(), State.Sessions.Values.ToArray(), maxConcurrentRequests);
            if (head is null)
            {
                return null;
            }

            ExecutionReservation reservation = new(Guid.NewGuid(), head.RequestId, head.SessionId, head.WriteWorkspaceKey, Clock.GetUtcNow());
            DispatchState.Snapshots.Add(reservation.DispatchId, new ReservedDispatch(reservation,
                State.Sessions[head.SessionId], State.Submissions[head.RequestId], head.Sequence, State.Sessions[head.SessionId].BindingRevision));
            State.Reservations.Add(reservation.DispatchId, reservation);
            State.Requests[head.RequestId] = State.Requests[head.RequestId] with { State = RequestState.Running, Certainty = ExecutionCertainty.InProgress };
            return reservation;
        }
    }
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
            State.Sessions[reservation.SessionId] = updated;
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
            State.Requests[reservation.RequestId] = State.Requests[reservation.RequestId] with
            {
                State = RequestState.Interrupted,
                Certainty = ExecutionCertainty.Unknown,
                Result = new(RequestState.Interrupted, ExecutionCertainty.Unknown, string.Empty, false, null,
                    failure with { Certainty = ExecutionCertainty.Unknown }),
            };
            State.Sessions[reservation.SessionId] = State.Sessions[reservation.SessionId] with { BlockReason = SessionBlockReason.RecoveryRequired };
            RecoveryState.Open(State.Sessions[reservation.SessionId], reservation, Clock.GetUtcNow(), DispatchState.OperationReference(dispatchId));
        }
    }
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, ProviderBinding? confirmedBinding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            RecoveryState.Resolve(decision, confirmedBinding, State.Sessions, State.Requests, State.Reservations);
        }
        return ValueTask.CompletedTask;
    }
}
