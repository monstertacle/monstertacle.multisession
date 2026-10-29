using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Recovery;

// Checks only locally declared support; native model/credential access remains an adapter concern.
internal sealed class RecoveryCoordinator(ISessionStore store, IReadOnlyList<ProviderRegistration> providers)
{
    private readonly FrozenDictionary<string, ProviderRegistration> _providers = providers.ToFrozenDictionary(item => item.ProviderId, StringComparer.Ordinal);

    internal async ValueTask PrepareAsync(CancellationToken cancellationToken)
    {
        string? cursor = null;
        do
        {
            Page<SessionSnapshot> page = await store.ListSessionsAsync(PageLimits.MaximumSize, cursor, cancellationToken);
            foreach (SessionSnapshot session in page.Items)
            {
                if (session.Lifecycle != SessionLifecycle.Open) { continue; }
                await store.CommitConfigurationAvailabilityAsync(session.SessionId, Available(session), cancellationToken);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

    private bool Available(SessionSnapshot session)
    {
        if (!_providers.TryGetValue(session.ProviderId, out ProviderRegistration? registration) || session.Binding is not { } binding
            || binding.AdapterId != registration.AdapterId || binding.FormatVersion != registration.ContractVersion) { return false; }
        ProviderCapabilities capabilities = registration.Provider.Capabilities;
        bool supported = session.Context.ExecutionKind switch
        {
            ExecutionKind.AgentTask => capabilities.SupportsAgentTasks,
            ExecutionKind.ModelResponse => capabilities.SupportsModelResponses,
            _ => false,
        };
        return supported && (!(capabilities.RequiresWorkspace || session.Context.ExecutionContext.WorkspaceDirectory is not null)
            || LocalDirectoryPath.IsValid(session.Context.ExecutionContext.WorkspaceDirectory));
    }
}
