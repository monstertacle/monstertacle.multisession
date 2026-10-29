using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Recovery;

// Establishes local compatibility and native reopen availability before queued input can dispatch.
internal sealed class RecoveryCoordinator(ISessionStore store, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions options, TimeProvider clock)
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
                ErrorCode? failure = Available(session) ? null : ErrorCode.InvalidConfiguration;
                if (failure is null && (session.BlockReason == SessionBlockReason.None
                    || (session.BlockReason == SessionBlockReason.ProviderUnavailable && session.ConfigurationUnavailable)))
                {
                    failure = await ReopenAsync(session, cancellationToken);
                }
                await store.CommitConfigurationAvailabilityAsync(session.SessionId, failure is null, cancellationToken, failure);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

    private async ValueTask<ErrorCode?> ReopenAsync(SessionSnapshot session, CancellationToken caller)
    {
        using CancellationTokenSource deadline = new(options.RequestTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token);
        try
        {
            ProviderBinding reopened = await _providers[session.ProviderId].Provider.ReopenAsync(session.Context, session.Binding!, linked.Token)
                .AsTask().WaitAsync(linked.Token);
            return reopened == session.Binding ? null : ErrorCode.ProviderSessionExpired;
        }
        catch (OperationCanceledException) when (caller.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return ErrorCode.TimedOut; }
        catch (Exception failure) { return ProviderFailurePolicy.Classify(failure); }
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
