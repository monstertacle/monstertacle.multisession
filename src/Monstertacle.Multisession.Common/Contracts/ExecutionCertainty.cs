namespace Monstertacle.Multisession.Contracts;

/// <summary>Evidence about external execution, independent of the local outcome.</summary>
public enum ExecutionCertainty
{
    /// <summary>No external execution was started.</summary>
    NotStarted,
    /// <summary>A local dispatch attempt is running.</summary>
    InProgress,
    /// <summary>External execution is confirmed to have ended.</summary>
    ConfirmedEnded,
    /// <summary>External execution may still be running.</summary>
    Unknown,
}
