using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Tracks dispatch-bound observation acknowledgements without advancing confirmed continuation.</summary>
internal sealed class DispatchObservationSink(ISessionStore store, Guid dispatchId, ProviderBinding binding, CancellationToken executionToken)
{
    private string? _sessionReference = binding.SessionReference;
    private string? _operationReference;
    private int _failed;
    private int _pending;
    private int _closed;
    private int _failureCode = (int)ErrorCode.StorageUnavailable;

    internal string? SessionReference => Volatile.Read(ref _sessionReference);
    internal string? OperationReference => Volatile.Read(ref _operationReference);
    internal bool HasUnconfirmedObservations => Volatile.Read(ref _failed) != 0 || Volatile.Read(ref _pending) != 0;
    internal ErrorCode FailureCode => (ErrorCode)Volatile.Read(ref _failureCode);

    internal async ValueTask RecordAsync(ProviderObservation observation, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            executionToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            ValidateIdentity(observation);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(executionToken, cancellationToken);
            await store.CommitObservationAsync(dispatchId, observation, linked.Token).AsTask().WaitAsync(linked.Token);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            if (observation.Kind == ProviderObservationKind.OperationAccepted) { Volatile.Write(ref _operationReference, observation.Reference); }
        }
        catch (Exception)
        {
            Interlocked.Exchange(ref _failed, 1);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
        }
    }

    internal void Close() => Interlocked.Exchange(ref _closed, 1);

    private void ValidateIdentity(ProviderObservation observation)
    {
        bool valid = observation.Kind switch
        {
            ProviderObservationKind.Progress => true,
            ProviderObservationKind.OperationAccepted => !string.IsNullOrWhiteSpace(observation.Reference),
            ProviderObservationKind.SessionEstablished => !string.IsNullOrWhiteSpace(observation.Reference)
                && (Interlocked.CompareExchange(ref _sessionReference, observation.Reference, null) is not { } current
                    || string.Equals(current, observation.Reference, StringComparison.Ordinal)),
            _ => false,
        };
        if (!valid)
        {
            Interlocked.Exchange(ref _failureCode, (int)ErrorCode.ProviderUnavailable);
            throw new SessionOperationException(new PublicError(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown));
        }
    }
}
