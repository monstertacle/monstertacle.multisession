using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession;

// Owns state, bounded workers and resource lifetime; request/session rules stay in their facades.
internal sealed class RuntimeLifecycle(RuntimeHost host) : IAsyncDisposable
{
    private readonly object _gate = new();
    private RuntimeComponents? _components;
    private Task[] _workers = [];
    private Task _completion = Task.CompletedTask;
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
        }
        bool prepared = false;
        try
        {
            await host.Store.StartAsync(host.Lease, cancellationToken);
            prepared = true;
            SessionCatalog catalog = new(host.Store, host.Providers, host.Profiles, host.Clock);
            await new RecoveryCoordinator(host.Store, host.Providers).PrepareAsync(cancellationToken);
            RequestAdmission admission = new(host.Store, options, host.Clock);
            RecoveryService recovery = new(host.Store, host.Providers, options);
            _components = new(new RuntimeSessions(this, catalog), new RuntimeRequests(this, admission),
                new RuntimeRecovery(this, recovery), new(host.Store, host.Providers, options, host.Clock), options);
            lock (_gate)
            {
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
        lock (_gate)
        {
            if (_state is RuntimeState.Stopped or RuntimeState.Disposed) { return; }
            if (_state is RuntimeState.Created or RuntimeState.Starting) { throw Error(ErrorCode.InvalidConfiguration); }
            _state = RuntimeState.Stopping;
        }
        using CancellationTokenSource deadline = new(timeout, host.Clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        SessionOperationException? failure = null;
        try
        {
            await host.Store.StopAcceptanceAsync(linked.Token).AsTask().WaitAsync(linked.Token);
            _stop!.Cancel();
            await _completion.WaitAsync(linked.Token);
        }
        catch (Exception) { failure = Error(ErrorCode.StorageUnavailable); }
        if (_completion.IsCompleted) { await CleanupAsync(); }
        if (failure is not null) { throw failure; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_state == RuntimeState.Disposed) { return; }
        if (_state == RuntimeState.Created || _components is null)
        {
            _state = RuntimeState.Disposed;
            return; // Dependencies remain host-owned until store preparation succeeds.
        }
        if (_state is not (RuntimeState.Stopped or RuntimeState.Disposed))
        {
            await StopAsync(Components.Options.ShutdownTimeout, CancellationToken.None);
        }
        _state = RuntimeState.Disposed;
    }

    private async ValueTask CleanupAsync()
    {
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
}
