using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Local deletion requires explicit retention and actual absence of active or uncertain work.</summary>
public sealed class RetentionPolicyTests
{
    private static readonly DateTimeOffset _closed = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>All lifecycle and work combinations are guarded independently from the selected explicit mode.</summary>
    [Fact]
    public void RequiresClosedWithoutActiveOrUncertainWork()
    {
        foreach (SessionLifecycle lifecycle in Enum.GetValues<SessionLifecycle>())
        {
            foreach (var (active, uncertain) in new[] { (false, false), (false, true), (true, false), (true, true) })
            {
                SessionSnapshot session = SchedulingData.Session() with { Lifecycle = lifecycle, ClosedUtc = _closed };
                Assert.Equal(lifecycle == SessionLifecycle.Closed && !active && !uncertain,
                    SessionRetentionPolicy.CanDelete(session, new(true, null), _closed, active, uncertain));
            }
        }
    }

    /// <summary>The selected finite duration is inclusive at its exact elapsed tick and independent of time-zone spelling.</summary>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void EnforcesExactDurationBoundary(long deltaTicks, bool expected)
    {
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Closed, ClosedUtc = _closed };
        DateTimeOffset now = (_closed + TimeSpan.FromDays(7)).AddTicks(deltaTicks).ToOffset(TimeSpan.FromHours(5));
        Assert.Equal(expected, SessionRetentionPolicy.CanDelete(session, new(false, TimeSpan.FromDays(7)), now, false, false));
    }

    /// <summary>No missing, contradictory or invalid policy can silently authorize deletion.</summary>
    [Fact]
    public void RejectsMissingOrInvalidRetentionAndMissingClosedTimestamp()
    {
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Closed, ClosedUtc = _closed };
        Contracts.RetentionPolicy?[] invalid = [null, new(false, null), new(true, TimeSpan.FromDays(1)), new(false, TimeSpan.Zero), new(false, TimeSpan.FromTicks(-1)), new(false, TimeSpan.MaxValue)];
        foreach (Contracts.RetentionPolicy? rule in invalid) { Assert.False(SessionRetentionPolicy.CanDelete(session, rule, _closed, false, false)); }
        Assert.False(SessionRetentionPolicy.CanDelete(session with { ClosedUtc = null }, new(true, null), _closed, false, false));
        Assert.False(SessionRetentionPolicy.CanDelete(session, new(false, TimeSpan.FromDays(1)), _closed.AddTicks(-1), false, false));
    }
}
