using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Provider-neutral continuation blocking after a safely committed terminal result.</summary>
public static class SessionBindingPolicy
{
    /// <summary>Preserves recovery priority and blocks known unusable continuation without inventing external termination.</summary>
    public static SessionBlockReason AfterCompletion(SessionBlockReason current, ProviderReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        if (current == SessionBlockReason.RecoveryRequired) { return current; }
        return reply.ContinuationAvailable is false || reply.Error?.Code == ErrorCode.ProviderSessionExpired
            ? SessionBlockReason.ProviderUnavailable : current;
    }
}
