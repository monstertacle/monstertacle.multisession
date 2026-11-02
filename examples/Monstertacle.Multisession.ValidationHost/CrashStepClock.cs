using System.Diagnostics;

namespace Monstertacle.Multisession.ValidationHost;

// Measures each named worker step; a step that throws stays the current step, so a failure names where it happened.
internal sealed class CrashStepClock
{
    private readonly List<CrashStep> _steps = [];

    internal string? Current { get; private set; }

    internal IReadOnlyList<CrashStep> Steps => _steps;

    internal async Task<T> MeasureAsync<T>(string name, Func<Task<T>> step)
    {
        Current = name;
        long started = Stopwatch.GetTimestamp();
        T result = await step();
        _steps.Add(new(name, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        Current = null;
        return result;
    }

    internal Task MeasureAsync(string name, Func<Task> step) => MeasureAsync(name, async () =>
    {
        await step();
        return true;
    });
}
