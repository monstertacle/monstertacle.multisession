namespace Monstertacle.Multisession.Contracts;

/// <summary>The local session lifecycle, independent of blocked external execution.</summary>
public enum SessionLifecycle
{
    /// <summary>Local identity exists and native opening has not been confirmed.</summary>
    Opening,
    /// <summary>The session can be considered for new work if it is not blocked.</summary>
    Open,
    /// <summary>Closing was requested, but unresolved work may still hold reservations.</summary>
    Closing,
    /// <summary>No open or uncertain work remains.</summary>
    Closed,
}
