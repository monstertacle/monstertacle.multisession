using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Checks explicit local deletion eligibility without claiming secure physical or external erasure.</summary>
public static class SessionRetentionPolicy
{
    /// <summary>Requires a closed conflict-free session and an explicitly selected, satisfied retention rule.</summary>
    public static bool CanDelete(SessionSnapshot session, Contracts.RetentionPolicy? rule, DateTimeOffset utcNow, bool active, bool uncertain)
    {
        if (session.Lifecycle != SessionLifecycle.Closed || session.ClosedUtc is not { } closed || active || uncertain || rule?.IsValid != true) { return false; }
        if (rule.RetainUntilExplicitDeletion) { return true; }
        return rule.Duration is { } duration && utcNow >= closed && utcNow - closed >= duration;
    }
}
