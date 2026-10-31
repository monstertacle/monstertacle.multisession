using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Scheduling;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled test state only. In-memory admission does not establish SQLite or crash durability.
internal sealed class InMemorySessionStore : ControlledCompletionStore, ISessionStore
{
    private readonly ControlledStoreState _state = new();
    protected override ControlledStoreState State => _state;
    public ControlledStoreState Data => State;
    public ControlledRecoveryState RecoveryState => State.Recovery;
    public Action? CommittingBinding { get; init; }
    public Action? BlockingSession { get; init; }
    public Func<SubmitRequest, CancellationToken, ValueTask>? CommittingAcceptance { get; init; }
    public Func<RequestAcceptance, CancellationToken, ValueTask>? Accepting { get; init; }
    public bool IgnoreAcceptanceCancellation { get; init; }
    public Action<RequestReceipt>? Accepted { get; init; }
    public IReadOnlyDictionary<Guid, SubmitRequest> Submissions => State.Submissions;
    public IReadOnlyDictionary<Guid, ExecutionReservation> Reservations => State.Reservations;
    public TimeProvider Clock { get => State.Clock; init => State.Clock = value; }
    public Func<CancellationToken, ValueTask>? Reserving { get; init; }

    public SessionSnapshot? FindSession(Guid sessionId)
    {
        lock (State.Gate) { return State.Sessions.TryGetValue(sessionId, out SessionSnapshot? session) ? session : null; }
    }

    public ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { State.Sessions.Add(session.SessionId, session); }
        return ValueTask.CompletedTask;
    }

    public ValueTask<SessionSnapshot> CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommittingBinding?.Invoke();
        lock (State.Gate)
        {
            SessionSnapshot session = State.Sessions[sessionId];
            if (session.Lifecycle is SessionLifecycle.Closing or SessionLifecycle.Closed) { throw new SessionOperationException(new(ErrorCode.SessionClosed)); }
            State.Sessions[sessionId] = session with
            {
                Binding = binding,
                Lifecycle = SessionLifecycle.Open,
                BindingRevision = session.Binding is null ? 0 : checked(session.BindingRevision + 1),
                UpdatedUtc = Clock.GetUtcNow().ToUniversalTime(),
            };
            return ValueTask.FromResult(State.Sessions[sessionId]);
        }
    }

    public ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BlockingSession?.Invoke();
        lock (State.Gate)
        {
            SessionSnapshot session = State.Sessions[sessionId] with { BlockReason = reason, UpdatedUtc = Clock.GetUtcNow().ToUniversalTime() };
            State.Sessions[sessionId] = session;
            if (reason == SessionBlockReason.RecoveryRequired && session.Lifecycle == SessionLifecycle.Opening)
            {
                RecoveryState.Open(session, null, Clock.GetUtcNow(), null);
            }
        }
        return ValueTask.CompletedTask;
    }
    public ValueTask CommitConfigurationAvailabilityAsync(Guid sessionId, bool available, CancellationToken cancellationToken, ErrorCode? failure = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            SessionSnapshot original = State.Sessions[sessionId];
            SessionSnapshot updated = SessionAvailabilityPolicy.ForConfiguration(original, available, failure);
            if (updated != original) { State.Sessions[sessionId] = updated with { UpdatedUtc = Clock.GetUtcNow().ToUniversalTime() }; }
        }
        return ValueTask.CompletedTask;
    }

    async ValueTask<RequestReceipt> ISessionStore.CommitAcceptanceAsync(RequestAcceptance acceptance, int maxQueuedRequests, CancellationToken cancellationToken)
    {
        if (Accepting is not null) { await Accepting(acceptance, cancellationToken); }
        return await AcceptAsync(acceptance, maxQueuedRequests, cancellationToken);
    }

    public ValueTask<RequestReceipt> CommitAcceptanceAsync(SubmitRequest request, int maxQueuedRequests, CancellationToken cancellationToken)
        => AcceptAsync(new RequestAcceptance(Guid.NewGuid(), request), maxQueuedRequests, cancellationToken);

    private async ValueTask<RequestReceipt> AcceptAsync(RequestAcceptance acceptance, int maxQueuedRequests, CancellationToken cancellationToken)
    {
        SubmitRequest request = acceptance.Submission;
        if (CommittingAcceptance is not null)
        {
            await CommittingAcceptance(request, cancellationToken);
        }

        if (!IgnoreAcceptanceCancellation) { cancellationToken.ThrowIfCancellationRequested(); }
        RequestReceipt receipt;
        lock (State.Gate)
        {
            if (!State.Accepting)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.SessionClosed, SessionId: request.SessionId, RequestId: acceptance.RequestId, AdmissionStatus: AdmissionStatus.NotAccepted));
            }
            if (!State.Sessions.TryGetValue(request.SessionId, out SessionSnapshot? session))
            {
                throw new SessionOperationException(new PublicError(ErrorCode.NotFound, SessionId: request.SessionId, AdmissionStatus: AdmissionStatus.NotAccepted));
            }

            if (session.Lifecycle != SessionLifecycle.Open || session.BlockReason != SessionBlockReason.None)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.SessionBlocked, SessionId: request.SessionId, AdmissionStatus: AdmissionStatus.NotAccepted));
            }

            if (State.Requests.Values.Count(item => item.State == RequestState.Queued) >= maxQueuedRequests)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.QueueFull, SessionId: request.SessionId, AdmissionStatus: AdmissionStatus.NotAccepted));
            }

            long sequence = State.Requests.Values.Where(item => item.SessionId == request.SessionId)
                .Select(item => item.Sequence).DefaultIfEmpty().Max() + 1;
            Guid requestId = acceptance.RequestId;
            if (requestId == Guid.Empty || State.Requests.ContainsKey(requestId))
            {
                throw new SessionOperationException(new PublicError(ErrorCode.InvalidInput, SessionId: request.SessionId, RequestId: requestId, AdmissionStatus: AdmissionStatus.NotAccepted));
            }
            State.Requests.Add(requestId, new RequestSnapshot(requestId, request.SessionId, sequence, RequestState.Queued, ExecutionCertainty.NotStarted, null) { SubmissionFingerprint = RequestSubmissionFingerprint.Compute(request) });
            State.Submissions.Add(requestId, request);
            State.AcceptedUtc.Add(requestId, Clock.GetUtcNow());
            receipt = new RequestReceipt(requestId, sequence);
        }

        Accepted?.Invoke(receipt);
        return receipt;
    }
    public async ValueTask<ExecutionReservation?> TryReserveHeadAsync(int maxConcurrentRequests, CancellationToken cancellationToken, Guid? expectedRequestId = null)
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
            if (head is null || (expectedRequestId is { } expected && head.RequestId != expected))
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
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, ProviderBinding? confirmedBinding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            RecoveryState.Resolve(decision, confirmedBinding, State.Sessions, State.Requests, State.Reservations, Clock.GetUtcNow().ToUniversalTime());
        }
        return ValueTask.CompletedTask;
    }
}
