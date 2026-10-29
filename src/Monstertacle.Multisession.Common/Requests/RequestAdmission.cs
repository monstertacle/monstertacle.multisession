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
    public async ValueTask<RequestReceipt> SubmitAsync(SubmitRequest request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(_options.AdmissionTimeout, _clock);
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        Guid? candidateId = null;
        bool committing = false;
        Task<RequestReceipt>? pending = null;
        try
        {
            execution.Token.ThrowIfCancellationRequested();
            SessionSnapshot? session = await _store.GetSessionAsync(request.SessionId, execution.Token).AsTask().WaitAsync(execution.Token);
            if (RequestSubmissionRules.Validate(request, session, _options) is { } failure) { throw new SessionOperationException(failure); }
            candidateId = Guid.NewGuid();
            committing = true;
            ValueTask<RequestReceipt> operation = _store.CommitAcceptanceAsync(new(candidateId.Value, request), _options.MaxQueuedRequests, execution.Token);
            if (operation.IsCompletedSuccessfully) { return ValidateReceipt(operation.Result, candidateId.Value); }
            pending = operation.AsTask();
            return ValidateReceipt(await pending.WaitAsync(execution.Token), candidateId.Value);
        }
        catch (OperationCanceledException failure)
        {
            if (pending is { IsCompletedSuccessfully: true }) { return ValidateReceipt(pending.Result, candidateId!.Value); }
            AdmissionStatus status = !committing || failure is AdmissionCancellationException { Failure.AdmissionStatus: AdmissionStatus.NotAccepted }
                ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown;
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdmissionCancellationException(Error(ErrorCode.Cancelled, request, candidateId, status), cancellationToken);
            }
            throw new SessionOperationException(Error(deadline.IsCancellationRequested ? ErrorCode.AdmissionTimedOut : ErrorCode.StorageUnavailable, request, candidateId, status));
        }
        catch (SessionOperationException failure)
        {
            AdmissionStatus status = !committing || failure.Failure.AdmissionStatus == AdmissionStatus.NotAccepted ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown;
            throw new SessionOperationException(Error(failure.Failure.Code, request, candidateId, status));
        }
        catch (Exception)
        {
            throw new SessionOperationException(Error(ErrorCode.StorageUnavailable, request, candidateId, committing ? AdmissionStatus.Unknown : AdmissionStatus.NotAccepted));
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
        => throw new NotImplementedException();

    private static RequestReceipt ValidateReceipt(RequestReceipt receipt, Guid candidateId)
        => receipt.RequestId == candidateId && receipt.Sequence > 0 ? receipt
            : throw new SessionOperationException(new PublicError(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown, RequestId: candidateId, AdmissionStatus: AdmissionStatus.Unknown));
    private static PublicError Error(ErrorCode code, SubmitRequest request, Guid? candidateId, AdmissionStatus status)
        => new(code, status == AdmissionStatus.Unknown ? ExecutionCertainty.Unknown : ExecutionCertainty.NotStarted, request.SessionId, candidateId, status);
}
