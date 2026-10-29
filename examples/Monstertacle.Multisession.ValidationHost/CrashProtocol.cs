namespace Monstertacle.Multisession.ValidationHost;

// Test-only child protocol, linked into its parent harness from this single definition.
internal static class CrashProtocol
{
    internal const string WorkerArgument = "--crash-worker";
    internal const string JournalPoint = "sqlite-uncommitted";
    internal const string MarkerFileName = ".crash-probe-token";
    internal const string JournalBarrier = "sqlite-journal-spilled";
    internal const string RecoveryPoint = "session-recovery";
    internal const string RecoveryBarrier = "session-recovery-ready";
    internal const string ScopeFileName = ".crash-trustscope";
    internal const string ProviderId = "crash-provider";
    internal const string AdapterId = "crash-contract";
    internal const int AdapterVersion = 1;
    internal const string RunningInput = "running-b";
    internal const string BeforeAcceptance = "before-acceptance";
    internal const string AfterAcceptance = "after-acceptance";
    internal const string BeforeWake = "after-acceptance-before-wake";
    internal const string AfterReservation = "after-reservation-before-send";
    internal const string BeforeOperationId = "after-external-before-id";
    internal const string BeforeCompletion = "before-completion-commit";
    internal const string AfterCompletion = "after-completion-commit";

    internal static bool IsKnown(string point) => point is JournalPoint or RecoveryPoint or BeforeAcceptance or AfterAcceptance
        or BeforeWake or AfterReservation or BeforeOperationId or BeforeCompletion or AfterCompletion;
}

internal sealed record CrashSignal(string Barrier, int ProcessId, DateTimeOffset Utc)
{
    public CrashStateManifest? State { get; init; }
}

internal sealed record CrashStateManifest(Guid SessionA, Guid SessionB, Guid CompletedA, Guid CompletedB, IReadOnlyList<Guid> QueuedA, Guid RunningB);
