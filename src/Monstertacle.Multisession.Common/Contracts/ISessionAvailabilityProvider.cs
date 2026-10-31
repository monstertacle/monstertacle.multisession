namespace Monstertacle.Multisession.Contracts;

/// <summary>Optional read-only validation of the frozen model and current host credential before native reopening.</summary>
public interface ISessionAvailabilityProvider
{
    /// <summary>Rejects unavailable configured access without opening a session, generating output or replacing the selected model.</summary>
    ValueTask ValidateAvailabilityAsync(ProviderSessionContext context, CancellationToken cancellationToken);
}
