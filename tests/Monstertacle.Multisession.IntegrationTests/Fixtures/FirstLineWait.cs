using System.Diagnostics;
using System.Globalization;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// T023: the timed wait for a worker's first standard-output line. Standard error is read from the start into a bounded tail, so a
/// full pipe never blocks the worker. On a timeout the caller's own child is ended first (so the tail holds everything it wrote),
/// then a <see cref="TimeoutException"/> with the original as its inner exception names the limit, the elapsed time, that no worker
/// step is known (the worker reports its steps only in that line), the child's process id and the tail. A failure to end the child
/// is reported after it, never instead of it.
/// </summary>
internal sealed class FirstLineWait
{
    private const int _standardErrorTail = 2048;
    private readonly BoundedTextTail _standardError = new(_standardErrorTail);
    private readonly Process _child;
    private readonly TimeSpan _limit;

    // Subscribes before the child starts, so no standard-error line is missed.
    internal FirstLineWait(Process child, TimeSpan limit)
    {
        _child = child;
        _limit = limit;
        child.ErrorDataReceived += (_, received) => _standardError.Append(received.Data);
    }

    // Call once, after the child started.
    internal async Task<string?> ReadAsync(Func<Task> endChild)
    {
        _child.BeginErrorReadLine();
        long began = Stopwatch.GetTimestamp();
        try { return await _child.StandardOutput.ReadLineAsync().WaitAsync(_limit); }
        catch (TimeoutException timeout) { throw await AttributeAsync(timeout, Stopwatch.GetElapsedTime(began), endChild); }
    }

    private async Task<Exception> AttributeAsync(TimeoutException timeout, TimeSpan elapsed, Func<Task> endChild)
    {
        int processId = _child.Id;
        Exception? cleanup = null;
        try { await endChild(); }
        catch (Exception failure) { cleanup = failure; }
        TimeoutException attributed = new(string.Create(CultureInfo.InvariantCulture,
            $"Crash worker wrote no first line within {_limit.TotalSeconds:F1} s (elapsed {elapsed.TotalSeconds:F1} s); no worker step is known: the worker reports its steps only in that line. Its own worker process {processId} {(cleanup is null ? "ended" : "could not be ended (next exception)")}. {_standardError.Describe()}"), timeout);
        return cleanup is null ? attributed : new AggregateException("Ending the crash worker failed after its first-line timeout.", attributed, cleanup);
    }
}
