using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

// Owns state, bounded workers and resource lifetime; request/session rules stay in their facades.
internal sealed class RuntimeLifecycle(RuntimeHost host) : IAsyncDisposable
{
    private readonly object _gate = new();
    private RuntimeComponents? _components;
    private Task[] _workers = [];
    private Task _completion = Task.CompletedTask;
    private readonly RuntimeShutdown _shutdown = new(host.Store);
    private CancellationTokenSource? _stop;
    private SemaphoreSlim? _signal;
    private volatile RuntimeState _state;

    internal bool IsRunning => _state == RuntimeState.Running;
    internal RuntimeComponents Components => _components ?? throw Error(ErrorCode.InvalidConfiguration);
    internal Task Completion => _completion;

    internal void RequireRunning()
    {
        if (!IsRunning) { throw Error(ErrorCode.InvalidConfiguration); }
    }
    internal ValueTask<T> RunOperation<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken caller)
    {
        lock (_gate)
        {
            RequireRunning();
            return new(_shutdown.Operations.Run(operation, caller));
        }
    }
    internal void RequireReadable()
    {
        if (_state is not (RuntimeState.Running or RuntimeState.Stopping)) { throw Error(ErrorCode.InvalidConfiguration); }
    }
    internal void Pulse()
    {
        lock (_gate)
        {
            if (_state == RuntimeState.Running && _signal is not null && _signal.CurrentCount < _workers.Length) { _signal.Release(); }
        }
    }

    internal async ValueTask StartAsync(RuntimeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Validate() is { } failure) { throw new SessionOperationException(failure); }
        if (!host.Lease.IsHeld || !host.Lease.Matches(options.StateDirectory)) { throw Error(ErrorCode.InvalidConfiguration); }
        lock (_gate)
        {
            if (_state != RuntimeState.Created) { throw Error(ErrorCode.InvalidConfiguration); }
            _state = RuntimeState.Starting;
            _shutdown.Startup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        bool prepared = false;
        try
        {
            await host.Store.ValidateRuntimeOptionsAsync(options, cancellationToken).AsTask().WaitAsync(cancellationToken);
            await host.Store.StartAsync(host.Lease, cancellationToken);
            prepared = true;
            _components = await RuntimePreparation.PrepareAsync(host, this, options, cancellationToken);
            lock (_gate)
            {
                if (_shutdown.DisposeRequested) { throw Error(ErrorCode.InvalidConfiguration); }
                _stop = new();
                _signal = new(0, options.MaxConcurrentRequests);
                _state = RuntimeState.Running;
                _workers = Enumerable.Range(0, options.MaxConcurrentRequests).Select(_ => Task.Run(WorkAsync)).ToArray();
                _completion = Task.WhenAll(_workers);
            }
        }
        catch (Exception startupFailure)
        {
            _state = RuntimeState.Faulted;
            if (prepared) { await CleanupAsync(); }
            cancellationToken.ThrowIfCancellationRequested();
            if (startupFailure is SessionOperationException known) { throw new SessionOperationException(known.Failure); }
            throw Error(ErrorCode.InvalidConfiguration);
        }
        finally { _shutdown.Startup!.TrySetResult(); }
    }

    private async Task WorkAsync()
    {
        try
        {
            while (!_stop!.IsCancellationRequested)
            {
                try
                {
                    if (await Components.Dispatcher.TryDispatchAsync(_stop.Token)) { continue; }
                    await _signal!.WaitAsync(RuntimeDefaults.SchedulerPollInterval, _stop.Token);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (SessionOperationException failure) when (failure.Failure.RequestId is not null && failure.Failure.Code != ErrorCode.StorageUnavailable)
                {
                    // A persisted local request failure is observable through request/recovery reads.
                }
            }
        }
        catch (Exception)
        {
            _state = RuntimeState.Faulted;
            _stop!.Cancel();
            throw Error(ErrorCode.StorageUnavailable);
        }
    }

    internal async ValueTask StopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!ExecutionDeadline.IsValid(timeout)) { throw Error(ErrorCode.InvalidInput); }
        using CancellationTokenSource deadline = new(timeout, host.Clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        lock (_gate)
        {
            if (_state == RuntimeState.Disposed) { return; }
            if (_state == RuntimeState.Stopped) { ThrowStopFailure(); return; }
            if (_state is RuntimeState.Created or RuntimeState.Starting) { throw Error(ErrorCode.InvalidConfiguration); }
            _state = RuntimeState.Stopping;
        }
        _shutdown.Operations.Stop();
        try { await _shutdown.EnterAsync(linked.Token); }
        catch (Exception) { throw Error(ErrorCode.StorageUnavailable); }
        try
        {
            if (_state is RuntimeState.Stopped or RuntimeState.Disposed) { ThrowStopFailure(); return; }
            await CompleteStopAsync(linked.Token);
        }
        finally { _shutdown.Release(); }
    }

    private async Task CompleteStopAsync(CancellationToken token)
    {
        SessionOperationException? failure = null;
        try
        {
            await host.Store.StopAcceptanceAsync(token).AsTask().WaitAsync(token);
            StopWorkers();
            await _shutdown.CancelQueuedAsync(token);
        }
        catch (Exception) { failure = Error(ErrorCode.StorageUnavailable); }
        try
        {
            StopWorkers();
            await _completion.WaitAsync(token);
        }
        catch (Exception) { failure = Error(ErrorCode.StorageUnavailable); }
        if (_completion.IsCompleted)
        {
            try { await CleanupAsync().WaitAsync(token); }
            catch (Exception) { failure = Error(ErrorCode.StorageUnavailable); }
        }
        _shutdown.Failure = failure?.Failure;
        if (failure is not null) { throw failure; }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_state == RuntimeState.Disposed) { return ValueTask.CompletedTask; }
            if (_shutdown.Disposal is { IsCompleted: false } pending) { return new(pending); }
            _shutdown.DisposeRequested = true;
            if (_state == RuntimeState.Created)
            {
                _state = RuntimeState.Disposed;
                _shutdown.Dispose();
                return ValueTask.CompletedTask;
            }
            if (_state == RuntimeState.Running) { _state = RuntimeState.Stopping; }
            Task startup = _shutdown.Startup?.Task ?? Task.CompletedTask;
            _shutdown.Disposal = Task.Run(() => DisposeCoreAsync(startup));
            return new(_shutdown.Disposal);
        }
    }

    private async Task DisposeCoreAsync(Task startup)
    {
        await startup;
        if (_state == RuntimeState.Disposed) { return; }
        if (_state == RuntimeState.Created || _components is null)
        {
            _state = RuntimeState.Disposed;
            _shutdown.Dispose();
            return; // Dependencies remain host-owned until store preparation succeeds.
        }
        if (_state is not (RuntimeState.Stopped or RuntimeState.Disposed))
        {
            await StopAsync(Components.Options.ShutdownTimeout, CancellationToken.None);
        }
        _state = RuntimeState.Disposed;
        _shutdown.Dispose();
    }

    private void StopWorkers()
    {
        lock (_gate)
        {
            if (_state is not (RuntimeState.Stopped or RuntimeState.Disposed)) { _stop?.Cancel(); }
        }
    }

    private Task CleanupAsync()
    {
        lock (_gate) { return _shutdown.Cleanup ??= CleanupCoreAsync(); }
    }

    private async Task CleanupCoreAsync()
    {
        await _shutdown.Operations.DrainAsync();
        if (_components is { } components) { await components.Statistics.DisposeAsync(); }
        await host.Store.DisposeAsync();
        await host.Lease.DisposeAsync();
        lock (_gate)
        {
            _signal?.Dispose();
            _stop?.Dispose();
            _state = RuntimeState.Stopped;
        }
    }

    private static SessionOperationException Error(ErrorCode code)
        => new(new PublicError(code, code == ErrorCode.StorageUnavailable ? ExecutionCertainty.Unknown : ExecutionCertainty.NotStarted));

    private void ThrowStopFailure()
    {
        if (_shutdown.Failure is { } failure) { throw new SessionOperationException(failure); }
    }
}
