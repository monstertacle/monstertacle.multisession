using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Diagnostics;

// Owns asynchronous sampling. Observable metric callbacks never enter the store queue.
internal sealed class RuntimeStatisticsMonitor : IAsyncDisposable
{
    private readonly ISessionStatisticsStore _store;
    private readonly SessionDiagnostics? _diagnostics;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    internal RuntimeStatisticsMonitor(ISessionStatisticsStore store, SessionDiagnostics? diagnostics)
    {
        _store = store;
        _diagnostics = diagnostics;
        _loop = diagnostics is null ? Task.CompletedTask : Task.Run(ObserveAsync);
    }

    private async Task ObserveAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    RuntimeStatistics snapshot = await _store.GetRuntimeStatisticsAsync(_stop.Token).AsTask().WaitAsync(_stop.Token);
                    _diagnostics!.UpdateRuntimeStatistics(snapshot);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception) { _diagnostics!.StatisticsUnavailable(); }
                await Task.Delay(TimeSpan.FromSeconds(1), _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _loop;
        _diagnostics?.UpdateRuntimeStatistics(null);
        _stop.Dispose();
    }
}
