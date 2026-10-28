using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Host-neutral durable acceptance; it never invokes an execution adapter.</summary>
public sealed class RequestAdmission : IRequests
{
    private readonly ISessionStore _store;
    private readonly RuntimeOptions _options;

    /// <summary>Composes the domain store and limits; behavior is added with its Red tests.</summary>
    public RequestAdmission(ISessionStore store, RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        PublicError? failure = options.Validate();
        if (failure is not null)
        {
            throw new SessionOperationException(failure);
        }

        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask<RequestReceipt> SubmitAsync(SubmitRequest request, CancellationToken cancellationToken)
    {
        SessionSnapshot? session = await _store.GetSessionAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            throw Failure(ErrorCode.NotFound, request.SessionId);
        }

        if (session.Lifecycle is SessionLifecycle.Closing or SessionLifecycle.Closed)
        {
            throw Failure(ErrorCode.SessionClosed, request.SessionId);
        }

        if (session.Lifecycle != SessionLifecycle.Open || session.BlockReason != SessionBlockReason.None)
        {
            throw Failure(ErrorCode.SessionBlocked, request.SessionId);
        }

        PublicError? inputFailure = Utf8MessageLimits.ValidateInput(request.Input, _options.MaxInputBytes);
        if (inputFailure is not null)
        {
            throw new SessionOperationException(inputFailure with { SessionId = request.SessionId });
        }

        if (!ExecutionDeadline.IsValid(request.Timeout))
        {
            throw Failure(ErrorCode.InvalidInput, request.SessionId);
        }

        if (request.WriteIntent
            && (string.IsNullOrWhiteSpace(request.WorkspaceKey)
                || (session.Context.ExecutionContext.WorkspaceKey is { } boundKey
                    && !string.Equals(boundKey, request.WorkspaceKey, StringComparison.Ordinal))))
        {
            throw Failure(ErrorCode.InvalidInput, request.SessionId);
        }

        // Do not re-check caller cancellation after the store confirms a successful commit.
        return await _store.CommitAcceptanceAsync(request, _options.MaxQueuedRequests, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => _store.GetRequestAsync(requestId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    private static SessionOperationException Failure(ErrorCode code, Guid sessionId)
        => new(new PublicError(code, SessionId: sessionId));
}
