namespace Monstertacle.Multisession.Contracts;

/// <summary>The local lifecycle state of an accepted request.</summary>
public enum RequestState
{
    /// <summary>Durably accepted and waiting.</summary>
    Queued,
    /// <summary>Dispatched locally.</summary>
    Running,
    /// <summary>Completed successfully with confirmed external end.</summary>
    Succeeded,
    /// <summary>Ended locally with a failure.</summary>
    Failed,
    /// <summary>Ended locally following cancellation.</summary>
    Cancelled,
    /// <summary>Ended locally at its execution deadline.</summary>
    TimedOut,
    /// <summary>Interrupted by an unclean host termination.</summary>
    Interrupted,
}
