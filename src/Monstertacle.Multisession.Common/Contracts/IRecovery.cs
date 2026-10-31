namespace Monstertacle.Multisession.Contracts;

/// <summary>Explicit investigation and revision-bound recovery, never implicit replay.</summary>
public interface IRecovery
{
    /// <summary>Lists unresolved execution reservations.</summary>
    ValueTask<Page<RecoveryCaseSnapshot>> ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken);
    /// <summary>Reads current recovery evidence, including its decision revision.</summary>
    ValueTask<RecoveryCaseSnapshot?> GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken);
    /// <summary>Reads native state without changing local outcomes or reservations.</summary>
    ValueTask<ProviderReply> InspectRecoveryAsync(RecoveryTarget target, CancellationToken cancellationToken);
    /// <summary>Applies an explicit operator decision with concurrency protection.</summary>
    ValueTask ResolveRecoveryAsync(RecoveryDecision decision, CancellationToken cancellationToken);
}
