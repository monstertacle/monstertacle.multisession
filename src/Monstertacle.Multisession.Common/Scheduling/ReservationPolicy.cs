using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Scheduling;

/// <summary>Selects an eligible session head; the store must apply selection and reservation atomically.</summary>
public static class ReservationPolicy
{
    /// <summary>Reads a consistent view of all queued requests, unreleased reservations and current sessions.</summary>
    public static QueuedRequest? SelectHead(
        IReadOnlyCollection<QueuedRequest> queued,
        IReadOnlyCollection<ExecutionReservation> unreleased,
        IReadOnlyCollection<SessionSnapshot> sessions,
        int maxConcurrentRequests)
    {
        if (maxConcurrentRequests <= 0)
        {
            throw new SessionOperationException(new PublicError(ErrorCode.InvalidConfiguration));
        }

        if (unreleased.Count >= maxConcurrentRequests)
        {
            return null;
        }

        HashSet<Guid> availableSessions = sessions
            .Where(session => session.Lifecycle == SessionLifecycle.Open && session.BlockReason == SessionBlockReason.None)
            .Select(session => session.SessionId).ToHashSet();
        availableSessions.ExceptWith(unreleased.Select(reservation => reservation.SessionId));
        HashSet<string> occupiedWorkspaces = unreleased
            .Where(reservation => reservation.WorkspaceKey is not null)
            .Select(reservation => reservation.WorkspaceKey!).ToHashSet(StringComparer.Ordinal);

        // Form session heads before filtering; a blocked head must never be bypassed.
        return queued.GroupBy(request => request.SessionId)
            .Select(group => group.MinBy(request => request.Sequence)!)
            .Where(request => availableSessions.Contains(request.SessionId)
                && (request.WriteWorkspaceKey is null || !occupiedWorkspaces.Contains(request.WriteWorkspaceKey)))
            .OrderBy(request => request.AcceptedUtc)
            .ThenBy(request => request.RequestId.ToString("D"), StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
