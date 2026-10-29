namespace Monstertacle.Multisession.Contracts;

/// <summary>Optional evidence-aware continuation check for providers whose execution can move a native chain head.</summary>
public interface IExecutionRecoveryProvider
{
    /// <summary>Reads known native evidence and returns a verified continuation without opening or sending work.</summary>
    ValueTask<ProviderBinding> ReopenAfterExecutionAsync(ProviderSessionContext context, ProviderBinding knownBinding,
        string? operationReference, CancellationToken cancellationToken);
}
