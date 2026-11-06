using System.Diagnostics;
using System.Globalization;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// T023: the timed wait for a worker's first standard-output line. From the start, standard error is drained in fixed chunks into a
/// fixed ring tail (SDK-SQ-001): no line is assembled and nothing grows with the stream, and a full pipe never blocks the worker.
/// On a timeout the caller's own child is ended first, then the drain is awaited for a bounded time so the tail holds what the
/// child wrote; then a <see cref="TimeoutException"/> with the original as its inner exception names the limit, the elapsed time,
/// that no worker step is known (the worker reports its steps only in that line), the child's process id and the tail. A failure to
/// end the child is reported after it, never instead of it.
/// </summary>
internal sealed class FirstLineWait
{
    private const int _standardErrorTail = 2048;
    private const int _readChunk = 4096; // characters per read of standard error: the reader's only buffer besides the stream's own
    private static readonly TimeSpan _drainAfterEnd = TimeSpan.FromSeconds(5); // bound for reading what the ended child wrote
    private readonly BoundedTextTail _standardError = new(_standardErrorTail);
    private readonly Process _child;
    private readonly TimeSpan _limit;
    private Task _drain = Task.CompletedTask;
    // SDK-SQ-R1-001: how the drain ended; null for the end of the stream, otherwise only the exception's type name (no payload).
    private string? _drainFailure;

    internal FirstLineWait(Process child, TimeSpan limit)
    {
        _child = child;
        _limit = limit;
    }

    // The standard-error tail, for the SDK-SQ-001 control.
    internal BoundedTextTail StandardError => _standardError;

    // Call once, after the child started; the drain starts before the wait, so no standard error is missed or left in the pipe.
    internal async Task<string?> ReadAsync(Func<Task> endChild)
    {
        _drain = DrainAsync(_child.StandardError, _standardError, failure => _drainFailure = failure);
        long began = Stopwatch.GetTimestamp();
        try { return await _child.StandardOutput.ReadLineAsync().WaitAsync(_limit); }
        catch (TimeoutException timeout) { throw await AttributeAsync(timeout, Stopwatch.GetElapsedTime(began), endChild); }
    }

    // Runs until the end of the stream, or until reading fails. A failure is recorded (its type name only) and never escapes: the
    // timeout report says the standard error is incomplete, and the attributed timeout stays the thrown exception. The report is
    // built before the harness disposes the process, so a failure it sees is unexpected; a stream closed by that later disposal
    // ends the drain after the report and changes nothing reported.
    private static async Task DrainAsync(StreamReader reader, BoundedTextTail tail, Action<string> failed)
    {
        char[] buffer = new char[_readChunk];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0) { tail.Append(buffer.AsSpan(0, read)); }
        }
        catch (Exception failure)
        {
            failed(failure.GetType().Name);
        }
    }

    private async Task<Exception> AttributeAsync(TimeoutException timeout, TimeSpan elapsed, Func<Task> endChild)
    {
        int processId = _child.Id;
        Exception? cleanup = null;
        try { await endChild(); }
        catch (Exception failure) { cleanup = failure; }
        string drained = string.Empty;
        try { await _drain.WaitAsync(_drainAfterEnd); }
        catch (TimeoutException) { drained = " (read so far: the stream did not end within " + _drainAfterEnd.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s)"; }
        if (_drainFailure is { } readFailure) { drained += " (standard error incomplete: reading failed with " + readFailure + ")"; }
        TimeoutException attributed = new(string.Create(CultureInfo.InvariantCulture,
            $"Crash worker wrote no first line within {_limit.TotalSeconds:F1} s (elapsed {elapsed.TotalSeconds:F1} s); no worker step is known: the worker reports its steps only in that line. Its own worker process {processId} {(cleanup is null ? "ended" : "could not be ended (next exception)")}.{drained} {_standardError.Describe()}"), timeout);
        return cleanup is null ? attributed : new AggregateException("Ending the crash worker failed after its first-line timeout.", attributed, cleanup);
    }
}
