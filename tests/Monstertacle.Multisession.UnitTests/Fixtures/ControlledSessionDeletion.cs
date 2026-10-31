using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Explicit policy and graph projection only; the owning test store gate is held.
internal static class ControlledSessionDeletion
{
    internal static void Apply(ControlledStoreState state, Guid sessionId)
    {
        SessionSnapshot session = state.Sessions.GetValueOrDefault(sessionId) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
        bool active = state.Requests.Values.Any(item => item.SessionId == sessionId && item.State is RequestState.Queued or RequestState.Running)
            || state.Reservations.Values.Any(item => item.SessionId == sessionId);
        bool uncertain = state.Requests.Values.Any(item => item.SessionId == sessionId && item.Certainty == ExecutionCertainty.Unknown)
            || state.Recovery.OpenCases.Any(item => item.SessionId == sessionId);
        if (!SessionRetentionPolicy.CanDelete(session, state.RuntimeOptions?.Retention, state.Clock.GetUtcNow(), active, uncertain))
        {
            throw new SessionOperationException(new(ErrorCode.SessionBlocked));
        }
        state.Recovery.DeleteSession(sessionId);
        state.Dispatch.DeleteSession(sessionId);
        foreach (Guid requestId in state.Requests.Values.Where(item => item.SessionId == sessionId).Select(item => item.RequestId).ToArray())
        {
            state.Requests.Remove(requestId);
            state.Submissions.Remove(requestId);
            state.AcceptedUtc.Remove(requestId);
        }
        state.Sessions.Remove(sessionId);
    }
}
