using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled closure projection; the owner holds its state gate.
internal static class ControlledSessionClosure
{
    internal static SessionSnapshot Begin(ControlledStoreState state, Guid sessionId)
    {
        SessionSnapshot session = state.Sessions.GetValueOrDefault(sessionId) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
        if (session.Lifecycle == SessionLifecycle.Closed) { return session; }
        DateTimeOffset now = state.Clock.GetUtcNow();
        if (session.Lifecycle == SessionLifecycle.Opening) { state.Recovery.Open(session, null, now, null); }
        foreach (RequestSnapshot request in state.Requests.Values.Where(item => item.SessionId == sessionId && item.State is RequestState.Queued or RequestState.Running).ToArray())
        {
            ControlledRequestCancellation.Apply(state, request.RequestId);
        }
        if (session.Lifecycle != SessionLifecycle.Closing)
        {
            state.Sessions[sessionId] = session with { Lifecycle = SessionLifecycle.Closing, ClosedUtc = null, UpdatedUtc = now };
        }
        return Refresh(state, sessionId);
    }

    internal static SessionSnapshot Refresh(ControlledStoreState state, Guid sessionId)
    {
        SessionSnapshot session = state.Sessions.GetValueOrDefault(sessionId) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
        bool active = state.Requests.Values.Any(item => item.SessionId == sessionId && item.State is RequestState.Queued or RequestState.Running)
            || state.Reservations.Values.Any(item => item.SessionId == sessionId);
        bool uncertain = state.Requests.Values.Any(item => item.SessionId == sessionId && item.Certainty == ExecutionCertainty.Unknown)
            || state.Recovery.OpenCases.Any(item => item.SessionId == sessionId);
        SessionSnapshot updated = SessionClosurePolicy.Refresh(session, active, uncertain, state.Clock.GetUtcNow());
        state.Sessions[sessionId] = updated;
        return updated;
    }
}
