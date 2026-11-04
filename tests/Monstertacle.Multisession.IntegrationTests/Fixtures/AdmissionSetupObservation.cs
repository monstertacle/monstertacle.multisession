using System.Diagnostics;
using System.Globalization;
using System.Text;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

/// <summary>
/// Test-only diagnostic for one sequential default-budget setup loop (follow-up-windows-admission.md, M1 and M2). The passing path
/// keeps only the last ten submit durations. On a failure the call site captures a synchronous snapshot first, then reports one
/// line with a bounded read-only candidate lookup, and rethrows its original exception itself. A failing report is thrown together
/// with the primary failure, which comes first. Bounds, budgets, tokens and cleanup of the caller are not touched.
/// </summary>
internal sealed class AdmissionSetupObservation : IAsyncDisposable
{
    private static readonly TimeSpan _defaultLookupBound = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _samplerStopBound = TimeSpan.FromSeconds(1);
    private readonly Func<Guid, CancellationToken, ValueTask<RequestSnapshot?>> _lookup;
    private readonly Action<string> _write;
    private readonly TimeSpan _lookupBound;
    private readonly SchedulingSampler _sampler = new();
    private readonly RecentDurations _recent = new();
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly TimeSpan _gcBaseline = GC.GetTotalPauseDuration();
    private bool _reported;

    private AdmissionSetupObservation(Func<Guid, CancellationToken, ValueTask<RequestSnapshot?>> lookup, Action<string> write, TimeSpan lookupBound)
    {
        _lookup = lookup;
        _write = write;
        _lookupBound = lookupBound;
    }

    internal static AdmissionSetupObservation Start(Func<Guid, CancellationToken, ValueTask<RequestSnapshot?>> lookup, Action<string> write,
        TimeSpan? lookupBound = null) => new(lookup, write, lookupBound ?? _defaultLookupBound);

    internal void Record(long submitted) => _recent.Add(Stopwatch.GetElapsedTime(submitted).TotalMilliseconds);

    // Synchronous, first in the catch: nothing here awaits, looks up or writes.
    internal AdmissionFailurePoint Capture(int index, Guid candidate, long submitted, Exception failure)
    {
        long at = Stopwatch.GetTimestamp();
        return new(index, candidate, Stopwatch.GetElapsedTime(submitted, at).TotalMilliseconds, Stopwatch.GetElapsedTime(_started, at).TotalMilliseconds,
            _recent.ToArray(), ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount, (GC.GetTotalPauseDuration() - _gcBaseline).TotalMilliseconds,
            _sampler.Snapshot(at));
    }

    internal async Task ReportAsync(AdmissionFailurePoint point, Exception failure)
    {
        _reported = true;
        try
        {
            string stop = await _sampler.StopAsync(_samplerStopBound);
            string lookup = await CandidateLookup.ObserveAsync(_lookup, point.Candidate, _lookupBound);
            _write(Format(point, failure, stop, lookup));
        }
        catch (Exception diagnostic)
        {
            throw new AggregateException("Reporting the admission setup observation failed after the submit failed.", failure, diagnostic);
        }
    }

    // After a reported failure the sampler state is already in the line; throwing here would replace the primary failure.
    public async ValueTask DisposeAsync()
    {
        if (!_reported) { await _sampler.DisposeAsync(); }
    }

    private static string Format(AdmissionFailurePoint point, Exception failure, string stop, string lookup)
    {
        SessionOperationException? session = failure as SessionOperationException;
        StringBuilder line = new();
        line.Append(CultureInfo.InvariantCulture, $"admission-setup-observation failure index={point.Index} candidate={point.Candidate:D}");
        line.Append(CultureInfo.InvariantCulture, $" exception={failure.GetType().FullName} code={Field(session?.Failure.Code)} stage={Field(session?.Details?.Stage)}");
        line.Append(CultureInfo.InvariantCulture, $" status={Field(session?.Failure.AdmissionStatus)} certainty={Field(session?.Failure.Certainty)}");
        line.Append(CultureInfo.InvariantCulture, $" request={(session?.Failure.RequestId is { } id ? id.ToString("D") : "not-available")}");
        line.Append(CultureInfo.InvariantCulture, $" submit-ms={point.SubmitMs:F1} setup-ms={point.SetupMs:F1} recent-ms={Recent(point.RecentMs)}");
        line.Append(CultureInfo.InvariantCulture, $" threads={point.Threads} pending={point.Pending} gc-pause-delta-ms={point.GcPauseDeltaMs:F1}");
        line.Append(' ').Append(point.Sampler.Describe()).Append(" sampler-stop=").Append(stop).Append(" lookup=").Append(lookup);
        return line.ToString();
    }

    private static string Field<T>(T? value) where T : struct, Enum => value?.ToString() ?? "not-available";

    private static string Recent(double[] values)
        => values.Length == 0 ? "none" : string.Join(',', values.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)));
}

/// <summary>The failure-point snapshot, taken before any diagnostic await.</summary>
internal sealed record AdmissionFailurePoint(int Index, Guid Candidate, double SubmitMs, double SetupMs, double[] RecentMs, int Threads,
    long Pending, double GcPauseDeltaMs, SamplerSnapshot Sampler);

/// <summary>A ring of the last ten passing submit durations in milliseconds.</summary>
internal sealed class RecentDurations
{
    private const int _capacity = 10;
    private readonly double[] _values = new double[_capacity];
    private int _count;

    internal void Add(double milliseconds) => _values[_count++ % _capacity] = milliseconds;

    internal double[] ToArray()
        => [.. Enumerable.Range(Math.Max(0, _count - _capacity), Math.Min(_count, _capacity)).Select(index => _values[index % _capacity])];
}
