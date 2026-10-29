using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

// Drains durable queued input through the same atomic cancellation contract used by callers.
internal sealed class RuntimeShutdown(ISessionStore store) : IDisposable
{
    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private Task? _workerCancellation;
    internal Task? Cleanup { get; set; }
    internal PublicError? Failure { get; set; }
    internal TaskCompletionSource? Startup { get; set; }
    internal Task? Disposal { get; set; }
    internal bool DisposeRequested { get; set; }
    internal RuntimeOperations Operations { get; } = new();
    internal Task EnterAsync(CancellationToken token) => _stopGate.WaitAsync(token);
    internal void Release() => _stopGate.Release();
    internal void CancelWorkers(CancellationTokenSource source) => _workerCancellation ??= source.CancelAsync();
    internal async Task DrainAsync()
    {
        await Operations.DrainAsync();
        if (_workerCancellation is { } cancelling)
        {
            try { await cancelling; }
            catch (Exception) { } // Report only after resources have been safely released.
        }
    }
    internal void RequireSuccessfulCancellation()
    {
        if (_workerCancellation is { IsFaulted: true })
        {
            throw new SessionOperationException(new(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown));
        }
    }
    public void Dispose()
    {
        Operations.Dispose();
        _stopGate.Dispose();
    }

    internal async ValueTask CancelQueuedAsync(CancellationToken token)
    {
        string? cursor = null;
        do
        {
            Page<SessionSnapshot> page = await store.ListSessionsAsync(PageLimits.MaximumSize, cursor, token).AsTask().WaitAsync(token);
            foreach (SessionSnapshot session in page.Items) { await store.CancelQueuedSessionAsync(session.SessionId, token).AsTask().WaitAsync(token); }
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

}
