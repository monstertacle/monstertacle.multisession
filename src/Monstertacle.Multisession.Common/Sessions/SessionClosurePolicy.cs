using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Separates closure intent from the absence of active or uncertain work.</summary>
public static class SessionClosurePolicy
{
    /// <summary>Advances only a closing session; unknown work or cases prevent a final close.</summary>
    public static SessionSnapshot Refresh(SessionSnapshot session, bool active, bool uncertain, DateTimeOffset utcNow)
    {
        if (session.Lifecycle != SessionLifecycle.Closing) { return session; }
        DateTimeOffset now = utcNow.ToUniversalTime();
        bool closed = !active && !uncertain;
        SessionSnapshot updated = session with
        {
            Lifecycle = closed ? SessionLifecycle.Closed : SessionLifecycle.Closing,
            BlockReason = uncertain ? SessionBlockReason.RecoveryRequired : SessionBlockReason.None,
            ClosedUtc = closed ? session.ClosedUtc ?? now : null,
        };
        return updated == session ? session : updated with { UpdatedUtc = now };
    }
}
