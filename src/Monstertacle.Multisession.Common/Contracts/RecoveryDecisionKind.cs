namespace Monstertacle.Multisession.Contracts;

/// <summary>Explicit host recovery decisions, never automatic re-execution.</summary>
public enum RecoveryDecisionKind
{
    /// <summary>Confirm that external work has ended without inventing success.</summary>
    ConfirmEnded,
    /// <summary>Prevent new work while preserving unresolved reservations.</summary>
    CloseSession,
}
