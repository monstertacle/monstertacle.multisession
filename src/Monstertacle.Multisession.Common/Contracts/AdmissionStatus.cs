namespace Monstertacle.Multisession.Contracts;

/// <summary>Local acceptance certainty, independent from external execution evidence.</summary>
public enum AdmissionStatus
{
    /// <summary>The store operation did not commit a new request.</summary>
    NotAccepted,
    /// <summary>A successful commit and its actual receipt are known.</summary>
    Committed,
    /// <summary>Commit versus rollback is not known to the caller.</summary>
    Unknown,
}
