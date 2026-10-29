using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled contract state; the owning store gate serializes every operation.
internal sealed class ControlledRecoveryState
{
    private readonly Dictionary<RecoveryTarget, RecoveryCaseSnapshot> _cases = [];
    private readonly Dictionary<Guid, RecoveryTarget> _decisions = [];

    public RecoveryCaseSnapshot? Read(RecoveryTarget target) => _cases.GetValueOrDefault(target);
    public IReadOnlyList<RecoveryCaseSnapshot> OpenCases => _cases.Values.Where(item => item.Status == RecoveryCaseStatus.Open)
        .OrderBy(item => item.CreatedUtc).ThenBy(item => item.Target.Kind).ThenBy(item => item.Target.Id.ToString("D"), StringComparer.Ordinal).ToArray();

    public void Open(SessionSnapshot session, ExecutionReservation? reservation, DateTimeOffset now, string? operationReference, ProviderBinding? knownBinding = null)
    {
        RecoveryTarget target = reservation is null ? new(RecoveryTargetKind.SessionOpening, session.SessionId)
            : new(RecoveryTargetKind.RequestExecution, reservation.RequestId);
        RecoveryCaseSnapshot? old = Read(target);
        _cases[target] = new(target, session.SessionId, old?.CreatedUtc ?? now, old is null ? 1 : checked(old.Revision + 1))
        {
            RequestId = reservation?.RequestId,
            DispatchId = reservation?.DispatchId,
            Binding = knownBinding ?? session.Binding,
            OperationReference = operationReference,
        };
    }

    public void Resolve(RecoveryDecision decision, ProviderBinding? confirmedBinding,
        Dictionary<Guid, SessionSnapshot> sessions, Dictionary<Guid, RequestSnapshot> requests,
        Dictionary<Guid, ExecutionReservation> reservations, DateTimeOffset now)
    {
        if (!_cases.TryGetValue(decision.Target, out RecoveryCaseSnapshot? item)) { throw Failure(ErrorCode.NotFound); }
        if (_decisions.ContainsKey(decision.DecisionId) || decision.ExpectedRevision != item.Revision || item.Status != RecoveryCaseStatus.Open)
        {
            throw Failure(ErrorCode.RevisionConflict);
        }
        if (decision.DecisionId == Guid.Empty || string.IsNullOrWhiteSpace(decision.Explanation) || string.IsNullOrWhiteSpace(decision.EvidenceReference))
        {
            throw Failure(ErrorCode.InvalidInput);
        }
        SessionSnapshot session = sessions[item.SessionId];
        RecoveryCaseSnapshot updated = item with { Revision = checked(item.Revision + 1) };
        if (decision.Kind == RecoveryDecisionKind.CloseSession)
        {
            sessions[item.SessionId] = session with { Lifecycle = SessionLifecycle.Closing, UpdatedUtc = now };
        }
        else if (decision.Kind == RecoveryDecisionKind.ConfirmEnded)
        {
            if (session.Lifecycle != SessionLifecycle.Closing && !Compatible(item.Binding, confirmedBinding))
            {
                throw Failure(ErrorCode.ProviderUnavailable);
            }
            if (item.RequestId is { } requestId)
            {
                RequestSnapshot request = requests[requestId];
                requests[requestId] = request with
                {
                    Certainty = ExecutionCertainty.ConfirmedEnded,
                    Result = request.Result is null ? null : request.Result with { Certainty = ExecutionCertainty.ConfirmedEnded },
                };
            }
            if (item.DispatchId is { } dispatchId) { reservations.Remove(dispatchId); }
            updated = updated with { Status = RecoveryCaseStatus.Resolved, Certainty = ExecutionCertainty.ConfirmedEnded, BlockReason = SessionBlockReason.None };
            bool otherOpen = _cases.Values.Any(other => other.Target != item.Target && other.SessionId == item.SessionId && other.Status == RecoveryCaseStatus.Open);
            var (remaining, unknown) = Pending(item.SessionId, requests.Values, reservations.Values);
            bool uncertain = otherOpen || unknown;
            SessionSnapshot updatedSession = session with
            {
                Lifecycle = session.Lifecycle == SessionLifecycle.Closing ? SessionLifecycle.Closing : SessionLifecycle.Open,
                BlockReason = uncertain ? SessionBlockReason.RecoveryRequired : SessionBlockReason.None,
                Binding = confirmedBinding ?? session.Binding,
                BindingRevision = confirmedBinding is null ? session.BindingRevision : checked(session.BindingRevision + 1),
                UpdatedUtc = now,
                ClosedUtc = null,
            };
            sessions[item.SessionId] = SessionClosurePolicy.Refresh(updatedSession, remaining, uncertain, now);
        }
        else { throw Failure(ErrorCode.InvalidInput); }
        _cases[item.Target] = updated;
        _decisions.Add(decision.DecisionId, item.Target);
    }

    public void DeleteSession(Guid sessionId)
    {
        HashSet<RecoveryTarget> targets = _cases.Values.Where(item => item.SessionId == sessionId).Select(item => item.Target).ToHashSet();
        foreach (Guid id in _decisions.Where(item => targets.Contains(item.Value)).Select(item => item.Key).ToArray()) { _decisions.Remove(id); }
        foreach (RecoveryTarget target in targets) { _cases.Remove(target); }
    }

    private static bool Compatible(ProviderBinding? original, ProviderBinding? reopened)
        => original?.SessionReference is not null && reopened is not null
            && original.AdapterId == reopened.AdapterId && original.FormatVersion == reopened.FormatVersion
            && original.SessionReference == reopened.SessionReference;

    private static (bool Active, bool Unknown) Pending(Guid sessionId, IEnumerable<RequestSnapshot> requests, IEnumerable<ExecutionReservation> reservations)
        => (requests.Any(request => request.SessionId == sessionId && request.State is RequestState.Queued or RequestState.Running)
                || reservations.Any(reservation => reservation.SessionId == sessionId),
            requests.Any(request => request.SessionId == sessionId && request.Certainty == ExecutionCertainty.Unknown));

    private static SessionOperationException Failure(ErrorCode code) => new(new PublicError(code, ExecutionCertainty.Unknown));
}
