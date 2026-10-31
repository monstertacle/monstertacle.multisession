namespace Monstertacle.Multisession;

// Tracks public operations that may use the store after awaiting external provider I/O.
internal sealed class RuntimeOperations : IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<Task> _pending = [];
    private readonly CancellationTokenSource _stop = new();
    private Task? _cancelling;

    internal Task<T> Run<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken caller)
    {
        lock (_gate)
        {
            Task<T> task = Task.Run(async () =>
            {
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _stop.Token);
                linked.Token.ThrowIfCancellationRequested();
                return await operation(linked.Token);
            });
            return Track(task);
        }
    }

    internal Task<T> Track<T>(Task<T> task)
    {
        lock (_gate)
        {
            _pending.Add(task);
            _ = task.ContinueWith(completed =>
            {
                _ = completed.Exception; // Callers retain their own result; observe abandoned faults as well.
                lock (_gate) { _pending.Remove(completed); }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return task;
        }
    }

    internal void Stop()
    {
        lock (_gate) { _cancelling ??= _stop.CancelAsync(); }
    }

    internal async Task DrainAsync()
    {
        Stop();
        Task[] pending;
        Task cancelling;
        lock (_gate) { pending = [.. _pending]; cancelling = _cancelling!; }
        // Operation failures remain attached to the public calls; draining proves only local completion.
        try { await Task.WhenAll(pending); }
        catch (Exception) { }
        try { await cancelling; }
        catch (Exception) { }
    }

    public void Dispose() => _stop.Dispose();
}
