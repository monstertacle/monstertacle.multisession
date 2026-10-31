using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Host-neutral durable acceptance; it never invokes an execution adapter.</summary>
public sealed class RequestAdmission : IRequests
{
    private readonly ISessionStore _store;
    private readonly RuntimeOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Composes the authoritative store, limits and monotonic admission timer.</summary>
    public RequestAdmission(ISessionStore store, RuntimeOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Validate() is { } failure) { throw new SessionOperationException(failure); }
        _store = store;
        _options = options;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<RequestReceipt> SubmitAsync(Guid requestId, SubmitRequest request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(_options.AdmissionTimeout, _clock);
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        bool committing = false;
        Task<RequestReceipt>? pending = null;
        FailureStage stage = FailureStage.AdmissionLookup;
        try
        {
            execution.Token.ThrowIfCancellationRequested();
            if (requestId == Guid.Empty) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
            SessionSnapshot? session = await _store.GetSessionAsync(request.SessionId, execution.Token).AsTask().WaitAsync(execution.Token);
            if (RequestSubmissionRules.Validate(request, session, _options) is { } failure) { throw new SessionOperationException(failure); }
            committing = true;
            stage = FailureStage.AdmissionCommit;
            ValueTask<RequestReceipt> operation = _store.CommitAcceptanceAsync(new(requestId, request), _options.MaxQueuedRequests, execution.Token);
            if (operation.IsCompletedSuccessfully) { return ValidateReceipt(operation.Result, requestId); }
            pending = operation.AsTask();
            return ValidateReceipt(await pending.WaitAsync(execution.Token), requestId);
        }
        catch (OperationCanceledException failure)
        {
            if (pending is { IsCompletedSuccessfully: true }) { return ValidateReceipt(pending.Result, requestId); }
            AdmissionStatus status = !committing || failure is AdmissionCancellationException { Failure.AdmissionStatus: AdmissionStatus.NotAccepted }
                ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown;
            FailureDetails details = (failure as AdmissionCancellationException)?.Details ?? new(stage);
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdmissionCancellationException(Error(ErrorCode.Cancelled, request, requestId, status), cancellationToken) { Details = details };
            }
            throw new SessionOperationException(Error(deadline.IsCancellationRequested ? ErrorCode.AdmissionTimedOut : ErrorCode.StorageUnavailable, request, requestId, status)) { Details = details };
        }
        catch (SessionOperationException failure)
        {
            AdmissionStatus status = !committing || failure.Failure.AdmissionStatus == AdmissionStatus.NotAccepted ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown;
            throw new SessionOperationException(Error(failure.Failure.Code, request, requestId, status)) { Details = failure.Details ?? new(stage) };
        }
        catch (Exception)
        {
            throw new SessionOperationException(Error(ErrorCode.StorageUnavailable, request, requestId, committing ? AdmissionStatus.Unknown : AdmissionStatus.NotAccepted)) { Details = new(stage) };
        }
    }

    /// <inheritdoc />
    public ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => _store.GetRequestAsync(requestId, cancellationToken);
    /// <inheritdoc />
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new RequestQueries(_store).ListRequestsAsync(sessionId, pageSize, cursor, cancellationToken);
    /// <inheritdoc />
    public ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => new RequestCancellation(_store).CancelAsync(requestId, cancellationToken);

    private static RequestReceipt ValidateReceipt(RequestReceipt receipt, Guid requestId)
        => receipt.RequestId == requestId && receipt.Sequence > 0 ? receipt
            : throw new SessionOperationException(new PublicError(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown, RequestId: requestId, AdmissionStatus: AdmissionStatus.Unknown));
    private static PublicError Error(ErrorCode code, SubmitRequest request, Guid requestId, AdmissionStatus status)
        => new(code, status == AdmissionStatus.Unknown ? ExecutionCertainty.Unknown : ExecutionCertainty.NotStarted, request.SessionId, requestId, status);
}
