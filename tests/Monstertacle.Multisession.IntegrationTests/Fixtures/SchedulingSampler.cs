using System.Diagnostics;
using System.Globalization;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// Test-only 50 ms thread-pool tick that records when it became ready, how many samples completed, the last sample and the
/// largest lateness. It only samples: a missing, zero or low lag is not proof that scheduling was healthy, and a high lag shows
/// delayed scheduling without naming its cause. Stopping is bounded and a stuck or faulted loop is reported, never ignored.
/// </summary>
internal sealed class SchedulingSampler : IAsyncDisposable
{
    internal static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan _disposeBound = TimeSpan.FromSeconds(1);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Task _loop;
    private (long Ready, int Samples, long LastSample, double MaxLagMs) _state;
    private string? _stopped;

    internal SchedulingSampler(Func<TimeSpan, CancellationToken, Task>? delay = null, Task? startGate = null)
    {
        _delay = delay ?? Task.Delay;
        _loop = RunAsync(startGate ?? Task.CompletedTask);
    }

    // The observation time is read under the same lock that publishes samples, so no published sample is newer than it.
    internal SamplerSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new(_started, _state.Ready == 0 ? null : _state.Ready, _state.Samples, _state.Samples == 0 ? null : _state.LastSample, _state.MaxLagMs,
                Stopwatch.GetTimestamp());
        }
    }

    internal async Task<string> StopAsync(TimeSpan bound)
    {
        if (_stopped is not null) { return _stopped; }
        await _stop.CancelAsync();
        try
        {
            await _loop.WaitAsync(bound);
            _stopped = "stopped";
        }
        catch (TimeoutException)
        {
            _stopped = "stop-timeout";
            TaskObservation.ObserveLateFault(_loop);
        }
        catch (Exception failure) { _stopped = "faulted:" + failure.GetType().FullName; }
        return _stopped;
    }

    public async ValueTask DisposeAsync()
    {
        string result = await StopAsync(_disposeBound);
        if (result != "stopped") { throw new InvalidOperationException("The scheduling sampler did not stop cleanly: " + result); }
        _stop.Dispose();
    }

    private async Task RunAsync(Task startGate)
    {
        await Task.Yield();
        await startGate;
        long previous = Stopwatch.GetTimestamp();
        lock (_gate) { _state.Ready = previous; }
        while (!_stop.IsCancellationRequested)
        {
            try { await _delay(Period, _stop.Token); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { return; }
            long now = Stopwatch.GetTimestamp();
            double lag = Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds - Period.TotalMilliseconds;
            lock (_gate)
            {
                _state.MaxLagMs = _state.Samples == 0 ? lag : Math.Max(_state.MaxLagMs, lag);
                _state.Samples++;
                _state.LastSample = now;
            }
            previous = now;
        }
    }
}

/// <summary>
/// One sampler reading at its own observation time, which is never earlier than any sample it contains. It is described against
/// the earlier failure point: how long after it the sampler was read, and whether the last sample came after that point. The end
/// gap runs to the observation time. Unstarted or unsampled parts are written as such, never as zero.
/// </summary>
internal readonly record struct SamplerSnapshot(long Started, long? Ready, int Samples, long? LastSample, double MaxLagMs, long ObservedAt)
{
    internal string Describe(long failureAt)
    {
        string ready = Ready is { } readyAt ? Milliseconds(Started, readyAt) : "not-ready";
        string lag = Samples == 0 ? "not-sampled" : MaxLagMs.ToString("F1", CultureInfo.InvariantCulture);
        string gap = (LastSample ?? Ready) is { } since ? Milliseconds(since, ObservedAt) : "unknown";
        string afterFailure = LastSample is { } last ? (last > failureAt ? "yes" : "no") : "none";
        return string.Create(CultureInfo.InvariantCulture, $"sampler-observed-ms-after-failure={Milliseconds(failureAt, ObservedAt)} sampler-ready={ready}"
            + $" samples={Samples} max-lag-ms={lag} end-gap-ms={gap} last-sample-after-failure={afterFailure}");
    }

    private static string Milliseconds(long from, long to)
        => Stopwatch.GetElapsedTime(from, to).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
}
