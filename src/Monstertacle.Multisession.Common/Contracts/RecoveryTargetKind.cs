namespace Monstertacle.Multisession.Contracts;

/// <summary>The kind of uncertain external work needing an explicit decision.</summary>
public enum RecoveryTargetKind
{
    /// <summary>A possibly accepted native opening, identified by local session ID.</summary>
    SessionOpening,
    /// <summary>A possibly accepted execution, identified by local request ID.</summary>
    RequestExecution,
}
