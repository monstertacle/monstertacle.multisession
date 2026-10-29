using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

// Drains durable queued input through the same atomic cancellation contract used by callers.
internal sealed class RuntimeShutdown(ISessionStore store) : IDisposable
{
    private readonly SemaphoreSlim _stopGate = new(1, 1);
    internal Task? Cleanup { get; set; }
    internal PublicError? Failure { get; set; }
    internal Task EnterAsync(CancellationToken token) => _stopGate.WaitAsync(token);
    internal void Release() => _stopGate.Release();
    public void Dispose() => _stopGate.Dispose();

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
