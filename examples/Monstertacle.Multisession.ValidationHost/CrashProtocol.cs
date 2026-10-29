namespace Monstertacle.Multisession.ValidationHost;

// Test-only child protocol, linked into its parent harness from this single definition.
internal static class CrashProtocol
{
    internal const string WorkerArgument = "--crash-worker";
    internal const string JournalPoint = "sqlite-uncommitted";
    internal const string MarkerFileName = ".crash-probe-token";
    internal const string JournalBarrier = "sqlite-journal-spilled";
}

internal sealed record CrashSignal(string Barrier, int ProcessId, DateTimeOffset Utc);
