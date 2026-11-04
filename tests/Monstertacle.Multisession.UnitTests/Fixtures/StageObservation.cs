using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

/// <summary>
/// Test-only diagnostic: stage timestamps of one measured window, written once after the window ended, whether it passed or failed.
/// A mark only reads <see cref="Stopwatch.GetTimestamp"/>; formatting and output never run inside the window. A stage that was not
/// reached is written as not observed, never as an invented time. The line shows the last stages the test observed; it does not
/// show which internal runtime step was slow, and nothing is written if the process ends or the awaited work never returns.
/// </summary>
internal sealed class StageObservation(string task, params string[] stages)
{
    private const string _notObserved = "not-observed";
    private readonly long[] _stamps = new long[stages.Length];
    private long _origin;

    internal void Mark(int stage) => _stamps[stage] = Stopwatch.GetTimestamp();

    // A stamp taken elsewhere; zero means it was not observed.
    internal void Record(int stage, long stamp) => _stamps[stage] = stamp;

    internal async Task ObserveAsync(long origin, Func<Task> measured, Action<string> write, Action? collect = null)
    {
        _origin = origin;
        Exception? failure = null;
        try { await measured(); }
        catch (Exception primary)
        {
            failure = primary;
            throw;
        }
        finally { Emit(write, collect, failure); }
    }

    // A failing write never hides the measured failure: both are thrown together, the measured one first.
    private void Emit(Action<string> write, Action? collect, Exception? failure)
    {
        try
        {
            collect?.Invoke();
            write(Format(failure));
        }
        catch (Exception output) when (failure is not null)
        {
            throw new AggregateException("Writing the stage observation failed after the measured work failed.", failure, output);
        }
    }

    private string Format(Exception? failure)
    {
        StringBuilder line = new("stage-observation ");
        line.Append(task).Append(" outcome=").Append(failure is null ? "passed" : "failed:" + failure.GetType().FullName);
        for (int stage = 0; stage < stages.Length; stage++)
        {
            line.Append(' ').Append(stages[stage]);
            if (_stamps[stage] == 0) { line.Append('=').Append(_notObserved); continue; }
            line.Append("-ms=").Append(Stopwatch.GetElapsedTime(_origin, _stamps[stage]).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture));
        }
        return line.ToString();
    }
}
