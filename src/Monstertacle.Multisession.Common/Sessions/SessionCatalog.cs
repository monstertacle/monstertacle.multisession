using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Creates and locates local sessions using explicit host registrations.</summary>
public sealed class SessionCatalog : ISessions
{
    private readonly ISessionStore _store;
    private readonly TimeProvider _clock;
    private readonly ProviderCatalog _configuration;

    /// <summary>Composes session catalog dependencies without selecting a vendor by name.</summary>
    public SessionCatalog(ISessionStore store, IReadOnlyList<ProviderRegistration> registrations, IReadOnlyList<AgentProfile> profiles, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(profiles);
        _store = store;
        _clock = clock ?? TimeProvider.System;
        _configuration = new(registrations, profiles);
    }

    /// <inheritdoc />
    public async ValueTask<SessionSnapshot> CreateSessionAsync(string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken)
    {
        AgentProfile profile = _configuration.GetProfile(agentProfileId);
        ProviderRegistration registration = _configuration.GetProvider(profile.ProviderId);
        ValidateExecution(profile.ExecutionKind, registration.Provider.Capabilities, context);
        Guid sessionId = Guid.NewGuid();
        ProviderSessionContext providerContext = new(sessionId, profile.ModelId, profile.ExecutionKind, context, profile.Settings.Values);
        DateTimeOffset createdUtc = _clock.GetUtcNow().ToUniversalTime();
        SessionSnapshot session = new(sessionId, registration.ProviderId, profile.AgentProfileId, providerContext, null)
        {
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc,
            SettingsRevision = profile.Settings.Revision,
        };
        await _store.CreateSessionAsync(session, cancellationToken);
        ProviderBinding binding;
        try
        {
            binding = await registration.Provider.OpenAsync(providerContext, cancellationToken);
        }
        catch (Exception failure)
        {
            ErrorCode code = failure is OperationCanceledException ? ErrorCode.Cancelled : ErrorCode.ProviderUnavailable;
            throw await BlockUncertainOpeningAsync(sessionId, code);
        }

        if (binding.AdapterId != registration.AdapterId || binding.FormatVersion != registration.ContractVersion)
        {
            throw await BlockUncertainOpeningAsync(sessionId, ErrorCode.ProviderUnavailable);
        }

        try
        {
            return await _store.CommitSessionBindingAsync(sessionId, binding, cancellationToken);
        }
        catch (Exception)
        {
            throw await BlockUncertainOpeningAsync(sessionId, ErrorCode.StorageUnavailable);
        }
    }
    /// <inheritdoc />
    public ValueTask<SessionSnapshot?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => _store.GetSessionAsync(sessionId, cancellationToken);
    /// <inheritdoc />
    public ValueTask<Page<SessionSnapshot>> ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => new SessionQueries(_store).ListSessionsAsync(pageSize, cursor, cancellationToken);
    /// <inheritdoc />
    public ValueTask<Page<HistoryEntry>> ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new SessionQueries(_store).ReadHistoryAsync(sessionId, pageSize, cursor, cancellationToken);
    /// <inheritdoc />
    public ValueTask CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    /// <inheritdoc />
    public ValueTask DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    private async ValueTask<SessionOperationException> BlockUncertainOpeningAsync(Guid sessionId, ErrorCode code)
    {
        // Caller cancellation must not prevent recording that a native open may have been accepted.
        // The store is responsible for its own finite write and busy limits.
        try
        {
            await _store.MarkSessionBlockedAsync(sessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        }
        catch (Exception)
        {
            // The public failure must distinguish an unconfirmed block from one stored successfully.
            code = ErrorCode.StorageUnavailable;
        }
        return new SessionOperationException(new PublicError(code, ExecutionCertainty.Unknown, sessionId));
    }

    private static void ValidateExecution(ExecutionKind executionKind, ProviderCapabilities capabilities, SessionExecutionContext context)
    {
        bool supported = executionKind switch
        {
            ExecutionKind.AgentTask => capabilities.SupportsAgentTasks,
            ExecutionKind.ModelResponse => capabilities.SupportsModelResponses,
            _ => false,
        };
        if (!supported)
        {
            throw new SessionOperationException(new PublicError(ErrorCode.UnsupportedProviderAction));
        }

        if ((capabilities.RequiresWorkspace || context.WorkspaceDirectory is not null)
            && !LocalDirectoryPath.IsValid(context.WorkspaceDirectory))
        {
            throw new SessionOperationException(new PublicError(ErrorCode.InvalidConfiguration));
        }
    }
}
