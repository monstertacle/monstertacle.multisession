using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Scheduling;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled test state only. In-memory admission does not establish SQLite or crash durability.
internal sealed class InMemorySessionStore : ISessionStore
{
    private readonly Dictionary<Guid, SessionSnapshot> _sessions = [];
    private readonly object _admissionGate = new();
    private readonly Dictionary<Guid, RequestSnapshot> _requests = [];
    private readonly Dictionary<Guid, SubmitRequest> _submissions = [];
    private readonly Dictionary<Guid, DateTimeOffset> _acceptedUtc = [];
    private readonly Dictionary<Guid, ExecutionReservation> _reservations = [];
    public Action? CommittingBinding { get; init; }
    public Action? BlockingSession { get; init; }
    public Func<SubmitRequest, CancellationToken, ValueTask>? CommittingAcceptance { get; init; }
    public Action<RequestReceipt>? Accepted { get; init; }
    public IReadOnlyDictionary<Guid, SubmitRequest> Submissions => _submissions;
    public IReadOnlyDictionary<Guid, ExecutionReservation> Reservations => _reservations;
    public TimeProvider Clock { get; init; } = TimeProvider.System;
    public Func<CancellationToken, ValueTask>? Reserving { get; init; }

    public SessionSnapshot? FindSession(Guid sessionId) => _sessions.GetValueOrDefault(sessionId);

    public ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.Add(session.SessionId, session);
        return ValueTask.CompletedTask;
    }

    public ValueTask CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommittingBinding?.Invoke();
        _sessions[sessionId] = _sessions[sessionId] with { Binding = binding, Lifecycle = SessionLifecycle.Open };
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BlockingSession?.Invoke();
        _sessions[sessionId] = _sessions[sessionId] with { BlockReason = reason };
        return ValueTask.CompletedTask;
    }

    ValueTask<SessionSnapshot?> ISessionQueryStore.GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(FindSession(sessionId));
    }

    ValueTask<Page<SessionSnapshot>> ISessionQueryStore.ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    ValueTask<Page<HistoryEntry>> ISessionQueryStore.ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    ValueTask<RequestSnapshot?> ISessionQueryStore.GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_admissionGate)
        {
            return ValueTask.FromResult(_requests.GetValueOrDefault(requestId));
        }
    }
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
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
        lock (_admissionGate)
        {
            if (!_sessions.TryGetValue(request.SessionId, out SessionSnapshot? session))
            {
                throw new SessionOperationException(new PublicError(ErrorCode.NotFound, SessionId: request.SessionId));
            }

            if (session.Lifecycle != SessionLifecycle.Open || session.BlockReason != SessionBlockReason.None)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.SessionBlocked, SessionId: request.SessionId));
            }

            if (_requests.Values.Count(item => item.State == RequestState.Queued) >= maxQueuedRequests)
            {
                throw new SessionOperationException(new PublicError(ErrorCode.QueueFull, SessionId: request.SessionId));
            }

            long sequence = _requests.Values.Where(item => item.SessionId == request.SessionId)
                .Select(item => item.Sequence).DefaultIfEmpty().Max() + 1;
            Guid requestId = Guid.NewGuid();
            _requests.Add(requestId, new RequestSnapshot(requestId, request.SessionId, sequence, RequestState.Queued, ExecutionCertainty.NotStarted, null));
            _submissions.Add(requestId, request);
            _acceptedUtc.Add(requestId, Clock.GetUtcNow());
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
        lock (_admissionGate)
        {
            QueuedRequest[] queued = _requests.Values.Where(request => request.State == RequestState.Queued)
                .Select(request => new QueuedRequest(request.RequestId, request.SessionId, request.Sequence,
                    _acceptedUtc[request.RequestId], _submissions[request.RequestId].WriteIntent ? _submissions[request.RequestId].WorkspaceKey : null))
                .ToArray();
            QueuedRequest? head = ReservationPolicy.SelectHead(queued, _reservations.Values.ToArray(), _sessions.Values.ToArray(), maxConcurrentRequests);
            if (head is null)
            {
                return null;
            }

            ExecutionReservation reservation = new(Guid.NewGuid(), head.RequestId, head.SessionId, head.WriteWorkspaceKey, Clock.GetUtcNow());
            _reservations.Add(reservation.DispatchId, reservation);
            _requests[head.RequestId] = _requests[head.RequestId] with { State = RequestState.Running, Certainty = ExecutionCertainty.InProgress };
            return reservation;
        }
    }
    public ValueTask CommitObservationAsync(Guid dispatchId, ProviderObservation observation, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    public ValueTask CommitCompletionAsync(Guid dispatchId, ProviderReply reply, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    public ValueTask MarkUncertainAsync(Guid dispatchId, PublicError failure, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_admissionGate)
        {
            ExecutionReservation reservation = _reservations[dispatchId];
            _requests[reservation.RequestId] = _requests[reservation.RequestId] with
            {
                State = RequestState.Interrupted,
                Certainty = ExecutionCertainty.Unknown,
                Result = new(RequestState.Interrupted, ExecutionCertainty.Unknown, string.Empty, false, null,
                    failure with { Certainty = ExecutionCertainty.Unknown }),
            };
            _sessions[reservation.SessionId] = _sessions[reservation.SessionId] with { BlockReason = SessionBlockReason.RecoveryRequired };
        }

        return ValueTask.CompletedTask;
    }
    public ValueTask ResolveReservationAsync(RecoveryDecision decision, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
