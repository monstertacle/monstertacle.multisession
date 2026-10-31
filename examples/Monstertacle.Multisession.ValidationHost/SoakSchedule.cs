namespace Monstertacle.Multisession.ValidationHost;

// Monotonic scheduling only; a controlled clock is never evidence of an actual soak.
internal sealed class SoakSchedule(TimeProvider clock)
{
    private readonly long _started = clock.GetTimestamp();
    private const int _minutes = 60;
    internal TimeSpan Elapsed => clock.GetElapsedTime(_started);
    internal bool NeedsRequest => AcceptedCount < _minutes;
    internal bool IsComplete => !NeedsRequest && Elapsed >= TimeSpan.FromMinutes(_minutes);
    internal int AcceptedCount { get; private set; }
    internal TimeSpan LastAcceptedElapsed { get; private set; }
    internal TimeSpan UntilNext => Nonnegative(TimeSpan.FromMinutes(AcceptedCount) - Elapsed);
    internal TimeSpan UntilEnd => Nonnegative(TimeSpan.FromMinutes(_minutes) - Elapsed);
    internal void RecordAccepted()
    {
        TimeSpan elapsed = Elapsed;
        ValidationCheck.Require(NeedsRequest && elapsed >= TimeSpan.FromMinutes(AcceptedCount)
            && elapsed < TimeSpan.FromMinutes(AcceptedCount + 1), "InvalidSoakAcceptanceWindow");
        LastAcceptedElapsed = elapsed;
        AcceptedCount++;
    }
    private static TimeSpan Nonnegative(TimeSpan duration) => duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
}
