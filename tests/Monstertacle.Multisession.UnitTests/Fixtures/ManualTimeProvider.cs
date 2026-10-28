namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled clock for timestamp-based tests. Timer scheduling is added before deadline tests.
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override long GetTimestamp() => _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        _utcNow += elapsed;
        _timestamp += elapsed.Ticks;
    }
}
