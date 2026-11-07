using System.Diagnostics;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

/// <summary>One scenario admission as a failed report shows it: order, phase, monotonic start and duration, and the known outcome.</summary>
internal sealed record ScenarioAdmission(int Index, string Phase, double StartMs, double DurationMs, string Outcome,
    string? Code = null, string? Stage = null, string? AdmissionStatus = null, string? Certainty = null, Guid? RequestId = null);

/// <summary>
/// Test-only admission envelope for one ScenarioRunner scenario (follow-up-windows-admission.md M1, extended to scenario admissions
/// for SDK-PR41-WIN-RETENTION-ADMISSION-001). Each ProbeHost submit of the scenario records its phase, its monotonic start since the
/// scenario began, its duration and the facts the outcome itself states; only the last <see cref="MaximumAdmissions"/> are kept.
/// Nothing is looked up, retried or reclassified; the original exception is rethrown by the caller unchanged. No runtime, public
/// API, persisted state, bound or timeout is touched.
/// </summary>
internal sealed class ScenarioAdmissionTrace : IDisposable
{
    internal const int MaximumAdmissions = 10;
    private const int MaximumPhaseCharacters = 64;
    private static readonly AsyncLocal<ScenarioAdmissionTrace?> _current = new();
    private readonly ScenarioAdmissionTrace? _previous;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Queue<ScenarioAdmission> _recent = new();
    private int _next;

    private ScenarioAdmissionTrace(ScenarioAdmissionTrace? previous) => _previous = previous;

    internal static ScenarioAdmissionTrace? Current => _current.Value;

    internal static ScenarioAdmissionTrace Begin()
    {
        ScenarioAdmissionTrace trace = new(_current.Value);
        _current.Value = trace;
        return trace;
    }

    internal (int Index, string Phase, long Started) Start(string input)
    {
        string phase = "submit:" + (input.Length > MaximumPhaseCharacters ? input[..MaximumPhaseCharacters] : input);
        lock (_recent) { return (++_next, phase, Stopwatch.GetTimestamp()); }
    }

    internal void Accepted((int Index, string Phase, long Started) admission, Guid requestId) =>
        Add(new(admission.Index, admission.Phase, StartMs(admission.Started), Duration(admission.Started), "accepted", RequestId: requestId));

    internal void Failed((int Index, string Phase, long Started) admission, Exception failure)
    {
        (PublicError? error, FailureDetails? details) = failure switch
        {
            SessionOperationException operation => (operation.Failure, operation.Details),
            AdmissionCancellationException cancellation => (cancellation.Failure, cancellation.Details),
            _ => ((PublicError?)null, (FailureDetails?)null)
        };
        Add(new(admission.Index, admission.Phase, StartMs(admission.Started), Duration(admission.Started), "failed",
            error?.Code.ToString() ?? failure.GetType().Name, details?.Stage.ToString() ?? "not-available",
            error?.AdmissionStatus.ToString() ?? "not-available", error?.Certainty.ToString() ?? "not-available", error?.RequestId));
    }

    internal IReadOnlyList<ScenarioAdmission> Snapshot()
    {
        lock (_recent) { return [.. _recent]; }
    }

    public void Dispose() => _current.Value = _previous;

    private void Add(ScenarioAdmission admission)
    {
        lock (_recent)
        {
            if (_recent.Count == MaximumAdmissions) { _recent.Dequeue(); }
            _recent.Enqueue(admission);
        }
    }

    private double StartMs(long started) => Stopwatch.GetElapsedTime(_started, started).TotalMilliseconds;
    private static double Duration(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
