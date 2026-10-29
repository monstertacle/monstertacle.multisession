using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.Scheduling;

namespace Monstertacle.Multisession.Requests;

/// <summary>Executes only freshly reserved work using its frozen dispatch snapshot.</summary>
public sealed class RequestDispatcher
{
    private readonly ISessionStore _store;
    private readonly RequestScheduler _scheduler;
    private readonly FrozenDictionary<string, ProviderRegistration> _providers;
    private readonly int _maxOutputBytes;
    private readonly TimeProvider _clock;
    private readonly SessionDiagnostics? _diagnostics;

    /// <summary>Freezes host registrations and validates the runtime options without starting execution.</summary>
    public RequestDispatcher(ISessionStore store, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions options, TimeProvider? clock = null,
        SessionDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _scheduler = new RequestScheduler(store, options);
        _store = store;
        _providers = providers.ToFrozenDictionary(item => item.ProviderId, StringComparer.Ordinal);
        _maxOutputBytes = options.MaxOutputBytes;
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics;
    }

    /// <summary>Processes one new reservation. False means no eligible dispatch; true confirms a persisted local result, including a confirmed failure.</summary>
    public async ValueTask<bool> TryDispatchAsync(CancellationToken cancellationToken)
    {
        ExecutionReservation? reservation;
        try
        {
            reservation = await _scheduler.TryReserveAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new SessionOperationException(new PublicError(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown));
        }
        if (reservation is null)
        {
            return false;
        }

        DispatchDiagnostics trace = new(_diagnostics, reservation);
        ReservedDispatch? dispatch;
        try
        {
            dispatch = await _store.GetReservedDispatchAsync(reservation.DispatchId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PublicError error = new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
            await CommitAsync(reservation, new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null, error), trace);
            throw new SessionOperationException(error);
        }
        catch (Exception)
        {
            throw Failure(reservation, ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown);
        }
        if (await CancelBeforeSendAsync(reservation, trace, cancellationToken)) { return true; }
        if (dispatch is null)
        {
            return false;
        }

        if (!_providers.TryGetValue(dispatch.Session.ProviderId, out ProviderRegistration? registration)
            || dispatch.Session.Binding is not { } binding
            || binding.AdapterId != registration.AdapterId || binding.FormatVersion != registration.ContractVersion)
        {
            PublicError error = new(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
            await CommitAsync(reservation, new(RequestState.Failed, ExecutionCertainty.NotStarted, string.Empty, false, null, error) { ContinuationAvailable = false }, trace);
            throw new SessionOperationException(error);
        }

        ProviderReply reply = await SendAsync(dispatch, registration, trace, cancellationToken);
        await CommitAsync(reservation, reply, trace);
        return true;
    }

    private async ValueTask<ProviderReply> SendAsync(ReservedDispatch dispatch, ProviderRegistration registration, DispatchDiagnostics trace, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(dispatch.Submission.Timeout, _clock);
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        ExecutionReservation reservation = dispatch.Reservation;
        DispatchObservationSink observations = new(_store, reservation.DispatchId, dispatch.Session.Binding!, execution.Token);
        ProviderCall call = new(dispatch.Session.Context, reservation.RequestId, reservation.DispatchId,
            dispatch.Submission.Input, dispatch.Submission.Timeout, _maxOutputBytes);
        DispatchCancellationWatch cancellation = new(_store, reservation.RequestId);
        using CancellationTokenSource watchStop = new();
        Task watching = cancellation.RunAsync(execution, watchStop.Token);
        Task<ProviderReply>? sending = null;
        ProviderReply reply;
        try
        {
            sending = registration.Provider.SendAsync(call, dispatch.Session.Binding!, observations.RecordAsync, execution.Token).AsTask();
            reply = await sending.WaitAsync(execution.Token);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            reply = await CompleteCancellationAsync(dispatch, registration, observations, sending, trace,
                cancellation.HasFailed ? ErrorCode.StorageUnavailable : deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled);
        }
        catch (Exception failure)
        {
            throw await PreserveAsync(reservation, observations.HasUnconfirmedObservations ? observations.FailureCode : ProviderFailurePolicy.Classify(failure), trace);
        }
        finally
        {
            observations.Close();
            watchStop.Cancel();
            await watching;
        }

        if (observations.HasUnconfirmedObservations)
        {
            throw await PreserveAsync(reservation, observations.FailureCode, trace);
        }
        reply = DispatchReplyPolicy.Normalize(reply, dispatch.Session.Binding!, observations.SessionReference, _maxOutputBytes);
        if (reply.Certainty == ExecutionCertainty.Unknown)
        {
            if (execution.IsCancellationRequested && reply.Error?.Code != ErrorCode.StorageUnavailable)
            {
                return await CompleteCancellationAsync(dispatch, registration, observations, sending, trace,
                    deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled);
            }
            throw await PreserveAsync(reservation, reply.Error?.Code ?? ErrorCode.Interrupted, trace);
        }
        return reply;
    }

    private async ValueTask<ProviderReply> CompleteCancellationAsync(ReservedDispatch dispatch, ProviderRegistration registration,
        DispatchObservationSink observations, Task<ProviderReply>? sending, DispatchDiagnostics trace, ErrorCode cause)
    {
        if (cause != ErrorCode.StorageUnavailable && !observations.HasUnconfirmedObservations)
        {
            ProviderReply? confirmed = await new DispatchTermination(dispatch, registration, observations, _maxOutputBytes).ConfirmAsync(sending, cause);
            if (confirmed is not null) { return confirmed; }
        }
        throw await PreserveAsync(dispatch.Reservation, observations.HasUnconfirmedObservations ? observations.FailureCode : cause, trace);
    }

    private async ValueTask<bool> CancelBeforeSendAsync(ExecutionReservation reservation, DispatchDiagnostics trace, CancellationToken cancellationToken)
    {
        RequestSnapshot? request;
        try { request = await _store.GetRequestAsync(reservation.RequestId, cancellationToken).AsTask().WaitAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PublicError cancelled = new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
            await CommitAsync(reservation, new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null, cancelled), trace);
            throw new SessionOperationException(cancelled);
        }
        catch (Exception) { throw Failure(reservation, ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown); }
        if (request is not { State: RequestState.Running, CancellationRequestedUtc: not null }) { return false; }
        PublicError error = new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
        await CommitAsync(reservation, new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null, error), trace);
        return true;
    }

    private async ValueTask CommitAsync(ExecutionReservation reservation, ProviderReply reply, DispatchDiagnostics trace)
    {
        try
        {
            // A caller token cannot erase a known final result. Stores enforce their own finite write limits.
            await _store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None);
            trace.Complete(reply);
        }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.RevisionConflict)
        {
            throw Failure(reservation, ErrorCode.RevisionConflict, ExecutionCertainty.Unknown);
        }
        catch (Exception)
        {
            // No compensating overwrite: this transaction might already have committed successfully.
            throw Failure(reservation, ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown);
        }
    }

    private async ValueTask<SessionOperationException> PreserveAsync(ExecutionReservation reservation, ErrorCode code, DispatchDiagnostics trace)
    {
        try
        {
            await _store.MarkUncertainAsync(reservation.DispatchId,
                new PublicError(code, ExecutionCertainty.Unknown, reservation.SessionId, reservation.RequestId), CancellationToken.None);
            trace.Complete(RequestFailurePolicy.Uncertain(new(code, ExecutionCertainty.Unknown)));
        }
        catch (Exception)
        {
            code = ErrorCode.StorageUnavailable;
        }
        return Failure(reservation, code, ExecutionCertainty.Unknown);
    }

    private static SessionOperationException Failure(ExecutionReservation reservation, ErrorCode code, ExecutionCertainty certainty)
        => new(new PublicError(code, certainty, reservation.SessionId, reservation.RequestId));
}
