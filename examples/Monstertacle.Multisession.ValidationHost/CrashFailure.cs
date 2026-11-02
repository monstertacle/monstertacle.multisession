using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// The worker's failure line: a finite category plus the public, path-free facts needed to attribute it.
internal sealed record CrashFailure(string Status, string FailureCode)
{
    public string? Stage { get; init; }

    public string? AdmissionStatus { get; init; }

    public Guid? RequestId { get; init; }

    public string? Step { get; init; }

    public IReadOnlyList<CrashStep> Steps { get; init; } = [];

    internal static CrashFailure From(Exception failure, CrashStepClock clock)
    {
        SessionOperationException? operation = failure as SessionOperationException;
        string category = failure switch
        {
            SessionOperationException => operation!.Failure.Code.ToString(),
            TimeoutException => "WorkerTimeout",
            _ => "crash-fixture",
        };
        return new("failed", category)
        {
            Stage = operation?.Details?.Stage.ToString(),
            AdmissionStatus = operation?.Failure.AdmissionStatus?.ToString(),
            RequestId = operation?.Failure.RequestId,
            Step = clock.Current,
            Steps = [.. clock.Steps],
        };
    }
}
