using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled store preparation and resource ownership, independent of read projections.
internal abstract class ControlledStoreLifetime : IStoreBootstrap, IAsyncDisposable
{
    private bool _ownsState;
    private bool _disposed;
    protected abstract ControlledStoreState State { get; }
    public Func<ValueTask>? Disposing { get; init; }
    public Func<RuntimeOptions, CancellationToken, ValueTask>? ValidatingOptions { get; init; }
    async ValueTask IStoreBootstrap.ValidateRuntimeOptionsAsync(RuntimeOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ValidatingOptions is not null) { await ValidatingOptions(options, cancellationToken); }
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Validate() is { } failure) { throw new SessionOperationException(failure); }
        lock (State.Gate)
        {
            if (State.Started) { throw new SessionOperationException(new(ErrorCode.StoreAlreadyOwned)); }
            State.RuntimeOptions = options;
        }
    }

    ValueTask IStoreBootstrap.StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            if (_disposed || !lease.IsHeld) { throw new SessionOperationException(new PublicError(ErrorCode.InvalidConfiguration)); }
            if (State.Started) { throw new SessionOperationException(new PublicError(ErrorCode.StoreAlreadyOwned)); }
            ControlledStoreBootstrap.Recover(State);
            State.Started = true;
            State.Accepting = true;
            _ownsState = true;
        }
        return ValueTask.CompletedTask;
    }
    ValueTask IStoreBootstrap.StopAcceptanceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { State.Accepting = false; }
        return ValueTask.CompletedTask;
    }
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Disposing is not null) { await Disposing(); }
        lock (State.Gate)
        {
            if (_ownsState) { State.Started = false; State.Accepting = false; _ownsState = false; }
            _disposed = true;
        }
    }

}
