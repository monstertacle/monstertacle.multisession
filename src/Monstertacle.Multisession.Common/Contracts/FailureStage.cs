namespace Monstertacle.Multisession.Contracts;

/// <summary>A closed diagnostic phase, not proof of acceptance or external termination.</summary>
public enum FailureStage
{
    /// <summary>Preparing the private state file.</summary>
    StorePreparation,
    /// <summary>Validating an existing database.</summary>
    StorePreflight,
    /// <summary>Opening or configuring the database.</summary>
    StoreOpen,
    /// <summary>Waiting for the serial store owner.</summary>
    StoreQueue,
    /// <summary>Executing a store operation.</summary>
    StoreExecution,
    /// <summary>Executing a transaction before commit.</summary>
    StoreTransaction,
    /// <summary>Attempting transaction commit; acceptance certainty is separate.</summary>
    StoreCommit,
    /// <summary>Reading admission prerequisites.</summary>
    AdmissionLookup,
    /// <summary>Waiting for the acceptance operation.</summary>
    AdmissionCommit,
}
