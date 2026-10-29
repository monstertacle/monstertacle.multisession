using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;
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

    /// <summary>Freezes host registrations and validates the runtime options without starting execution.</summary>
    public RequestDispatcher(ISessionStore store, IReadOnlyList<ProviderRegistration> providers, RuntimeOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _scheduler = new RequestScheduler(store, options);
        _store = store;
        _providers = providers.ToFrozenDictionary(item => item.ProviderId, StringComparer.Ordinal);
        _maxOutputBytes = options.MaxOutputBytes;
        _clock = clock ?? TimeProvider.System;
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

        ReservedDispatch? dispatch;
        try
        {
            dispatch = await _store.GetReservedDispatchAsync(reservation.DispatchId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PublicError error = new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
            await CommitAsync(reservation, new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null, error));
            throw new SessionOperationException(error);
        }
        catch (Exception)
        {
            throw Failure(reservation, ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown);
        }
        if (await CancelBeforeSendAsync(reservation, cancellationToken)) { return true; }
        if (dispatch is null)
        {
            return false;
        }

        if (!_providers.TryGetValue(dispatch.Session.ProviderId, out ProviderRegistration? registration)
            || dispatch.Session.Binding is not { } binding
            || binding.AdapterId != registration.AdapterId || binding.FormatVersion != registration.ContractVersion)
        {
            PublicError error = new(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
            await CommitAsync(reservation, new(RequestState.Failed, ExecutionCertainty.NotStarted, string.Empty, false, null, error) { ContinuationAvailable = false });
            throw new SessionOperationException(error);
        }

        ProviderReply reply = await SendAsync(dispatch, registration, cancellationToken);
        await CommitAsync(reservation, reply);
        return true;
    }

    private async ValueTask<ProviderReply> SendAsync(ReservedDispatch dispatch, ProviderRegistration registration, CancellationToken cancellationToken)
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
        ProviderReply reply;
        try
        {
            reply = await registration.Provider.SendAsync(call, dispatch.Session.Binding!, observations.RecordAsync, execution.Token)
                .AsTask().WaitAsync(execution.Token);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            throw await PreserveAsync(reservation, cancellation.HasFailed ? ErrorCode.StorageUnavailable
                : deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled);
        }
        catch (Exception)
        {
            throw await PreserveAsync(reservation, observations.HasUnconfirmedObservations ? observations.FailureCode : ErrorCode.ExecutionFailed);
        }
        finally
        {
            observations.Close();
            watchStop.Cancel();
            await watching;
        }

        if (observations.HasUnconfirmedObservations)
        {
            throw await PreserveAsync(reservation, observations.FailureCode);
        }
        reply = DispatchReplyPolicy.Normalize(reply, dispatch.Session.Binding!, observations.SessionReference, _maxOutputBytes);
        if (reply.Certainty == ExecutionCertainty.Unknown)
        {
            throw await PreserveAsync(reservation, reply.Error?.Code ?? ErrorCode.Interrupted);
        }
        return reply;
    }

    private async ValueTask<bool> CancelBeforeSendAsync(ExecutionReservation reservation, CancellationToken cancellationToken)
    {
        RequestSnapshot? request;
        try { request = await _store.GetRequestAsync(reservation.RequestId, cancellationToken).AsTask().WaitAsync(cancellationToken); }
        catch (Exception) { throw Failure(reservation, ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown); }
        if (request is not { State: RequestState.Running, CancellationRequestedUtc: not null }) { return false; }
        PublicError error = new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, reservation.SessionId, reservation.RequestId);
        await CommitAsync(reservation, new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null, error));
        return true;
    }

    private async ValueTask CommitAsync(ExecutionReservation reservation, ProviderReply reply)
    {
        try
        {
            // A caller token cannot erase a known final result. Stores enforce their own finite write limits.
            await _store.CommitCompletionAsync(reservation.DispatchId, reply, CancellationToken.None);
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

    private async ValueTask<SessionOperationException> PreserveAsync(ExecutionReservation reservation, ErrorCode code)
    {
        try
        {
            await _store.MarkUncertainAsync(reservation.DispatchId,
                new PublicError(code, ExecutionCertainty.Unknown, reservation.SessionId, reservation.RequestId), CancellationToken.None);
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
