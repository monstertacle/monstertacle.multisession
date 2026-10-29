using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Tracks the cause of local configuration blocks without replacing native recovery evidence.</summary>
public static class SessionAvailabilityPolicy
{
    /// <summary>Applies a local registration check without modifying frozen binding or profile data.</summary>
    public static SessionSnapshot ForConfiguration(SessionSnapshot session, bool available)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Lifecycle != SessionLifecycle.Open) { return session; }
        if (available && session.ConfigurationUnavailable)
        {
            return session with
            {
                ConfigurationUnavailable = false,
                BlockReason = session.BlockReason == SessionBlockReason.ProviderUnavailable ? SessionBlockReason.None : session.BlockReason,
            };
        }
        return !available && session.BlockReason == SessionBlockReason.None
            ? session with { ConfigurationUnavailable = true, BlockReason = SessionBlockReason.ProviderUnavailable }
            : session;
    }
}
