using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Recovery;

/// <summary>Investigates explicit recovery targets without replay or replacing native sessions.</summary>
public sealed class RecoveryService : IRecovery
{
    private readonly ISessionStore _store;
    private readonly FrozenDictionary<string, ProviderRegistration> _providers;
    private readonly RuntimeOptions _options;

    /// <summary>Composes the authoritative store, explicitly registered adapters and finite limits.</summary>
    public RecoveryService(ISessionStore store, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Validate() is { } failure) { throw new SessionOperationException(failure); }
        _store = store;
        _providers = providers.ToFrozenDictionary(item => item.ProviderId, StringComparer.Ordinal);
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask<RecoveryCaseSnapshot?> GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
        => _store.GetRecoveryCaseAsync(target, cancellationToken);
    /// <inheritdoc />
    public ValueTask<Page<RecoveryCaseSnapshot>> ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => _store.ListRecoveryCasesAsync(pageSize, cursor, cancellationToken);
    /// <inheritdoc />
    public async ValueTask<ProviderReply> InspectRecoveryAsync(RecoveryTarget target, CancellationToken cancellationToken)
    {
        RecoveryCaseSnapshot item = await RequiredCaseAsync(target, cancellationToken);
        SessionSnapshot session = await RequiredSessionAsync(item.SessionId, cancellationToken);
        if (!_providers.TryGetValue(session.ProviderId, out ProviderRegistration? registration)
            || !registration.Provider.Capabilities.SupportsInspection || item.OperationReference is null)
        {
            return new(RequestState.Interrupted, ExecutionCertainty.Unknown, string.Empty, false, null,
                new PublicError(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown, item.SessionId, item.RequestId));
        }
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        try
        {
            ProviderReply reply = await registration.Provider.InspectAsync(session.Context, item.OperationReference, deadline.Token)
                .AsTask().WaitAsync(deadline.Token);
            return ProviderReplyPolicy.NormalizeOutput(reply, _options.MaxOutputBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw Failure(ErrorCode.ProviderUnavailable, item); }
    }

    /// <inheritdoc />
    public async ValueTask ResolveRecoveryAsync(RecoveryDecision decision, CancellationToken cancellationToken)
    {
        if (decision.DecisionId == Guid.Empty || decision.ExpectedRevision < 1
            || string.IsNullOrWhiteSpace(decision.Explanation) || string.IsNullOrWhiteSpace(decision.EvidenceReference))
        {
            throw new SessionOperationException(new PublicError(ErrorCode.InvalidInput));
        }
        RecoveryCaseSnapshot item = await RequiredCaseAsync(decision.Target, cancellationToken);
        if (decision.ExpectedRevision != item.Revision) { throw Failure(ErrorCode.RevisionConflict, item); }
        SessionSnapshot session = await RequiredSessionAsync(item.SessionId, cancellationToken);
        ProviderBinding? reopened = null;
        if (decision.Kind == RecoveryDecisionKind.ConfirmEnded && session.Lifecycle != SessionLifecycle.Closing)
        {
            reopened = await ReopenAsync(session, item, cancellationToken);
        }
        await _store.ResolveRecoveryAsync(decision, reopened, cancellationToken);
    }

    private async ValueTask<ProviderBinding> ReopenAsync(SessionSnapshot session, RecoveryCaseSnapshot item, CancellationToken cancellationToken)
    {
        ProviderBinding? knownBinding = item.Binding ?? session.Binding;
        if (knownBinding?.SessionReference is null) { throw Failure(ErrorCode.UnsupportedProviderAction, item); }
        if (!_providers.TryGetValue(session.ProviderId, out ProviderRegistration? registration)
            || registration.AdapterId != knownBinding.AdapterId || registration.ContractVersion != knownBinding.FormatVersion)
        {
            throw Failure(ErrorCode.ProviderUnavailable, item);
        }
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        IExecutionRecoveryProvider? operationRecovery = registration.Provider as IExecutionRecoveryProvider;
        if (registration.Provider.Capabilities.RequiresOperationAwareRecovery && operationRecovery is null)
        {
            throw Failure(ErrorCode.UnsupportedProviderAction, item);
        }
        ProviderBinding reopened;
        try
        {
            ValueTask<ProviderBinding> operation = registration.Provider.Capabilities.RequiresOperationAwareRecovery
                ? operationRecovery!.ReopenAfterExecutionAsync(session.Context, knownBinding, item.OperationReference, deadline.Token)
                : registration.Provider.ReopenAsync(session.Context, knownBinding, deadline.Token);
            reopened = await operation.AsTask().WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.UnsupportedProviderAction)
        {
            throw Failure(ErrorCode.UnsupportedProviderAction, item);
        }
        catch (Exception) { throw Failure(ErrorCode.ProviderUnavailable, item); }
        if (reopened.AdapterId != knownBinding.AdapterId || reopened.FormatVersion != knownBinding.FormatVersion
            || reopened.SessionReference != knownBinding.SessionReference)
        {
            throw Failure(ErrorCode.ProviderUnavailable, item);
        }
        return reopened;
    }

    private async ValueTask<RecoveryCaseSnapshot> RequiredCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
        => await _store.GetRecoveryCaseAsync(target, cancellationToken)
            ?? throw new SessionOperationException(new PublicError(ErrorCode.NotFound));
    private async ValueTask<SessionSnapshot> RequiredSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => await _store.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new SessionOperationException(new PublicError(ErrorCode.NotFound));
    private static SessionOperationException Failure(ErrorCode code, RecoveryCaseSnapshot item)
        => new(new PublicError(code, ExecutionCertainty.Unknown, item.SessionId, item.RequestId));
}
