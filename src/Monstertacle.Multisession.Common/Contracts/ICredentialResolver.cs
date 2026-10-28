namespace Monstertacle.Multisession.Contracts;

/// <summary>Host-owned credential resolution, never persisted in session snapshots.</summary>
public interface ICredentialResolver
{
    /// <summary>Resolves a credential at call time without logging or persisting it.</summary>
    ValueTask<string> ResolveAsync(string credentialReference, CancellationToken cancellationToken);
}
