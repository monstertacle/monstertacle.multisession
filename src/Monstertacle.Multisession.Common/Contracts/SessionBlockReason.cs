namespace Monstertacle.Multisession.Contracts;

/// <summary>Why a local session cannot proceed; closing it is not evidence of external termination.</summary>
public enum SessionBlockReason
{
    /// <summary>No blocking reason is recorded.</summary>
    None,
    /// <summary>External state is uncertain and requires explicit investigation.</summary>
    RecoveryRequired,
    /// <summary>The configured external execution adapter is unavailable.</summary>
    ProviderUnavailable,
    /// <summary>The store cannot safely accept or execute work.</summary>
    StorageFailure,
}
