using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Tracks the cause of local configuration blocks without replacing native recovery evidence.</summary>
public static class SessionAvailabilityPolicy
{
    /// <summary>Applies a local registration check without modifying frozen binding or profile data.</summary>
    public static SessionSnapshot ForConfiguration(SessionSnapshot session, bool available, ErrorCode? failure = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Lifecycle != SessionLifecycle.Open) { return session; }
        if (available && session.ConfigurationUnavailable)
        {
            return session with
            {
                ConfigurationUnavailable = false,
                AvailabilityError = null,
                BlockReason = session.BlockReason == SessionBlockReason.ProviderUnavailable ? SessionBlockReason.None : session.BlockReason,
            };
        }
        return !available && (session.BlockReason == SessionBlockReason.None || session.ConfigurationUnavailable)
            ? session with
            {
                ConfigurationUnavailable = true,
                AvailabilityError = ProviderFailurePolicy.NormalizeCode(failure ?? ErrorCode.InvalidConfiguration),
                BlockReason = session.BlockReason == SessionBlockReason.None ? SessionBlockReason.ProviderUnavailable : session.BlockReason,
            }
            : session;
    }
}
