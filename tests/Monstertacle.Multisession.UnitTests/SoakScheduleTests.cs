using Monstertacle.Multisession.UnitTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Controlled-clock tests cover scheduling, never replace the real one-hour platform proof.</summary>
public sealed class SoakScheduleTests
{
    /// <summary>A successful runtime start begins a full hour and immediately requires its first accepted request.</summary>
    [Fact]
    public void StartsIncompleteWithFirstRequestDue()
    {
        SoakSchedule schedule = new(new ManualTimeProvider());
        Assert.True(schedule.NeedsRequest);
        Assert.False(schedule.IsComplete);
        Assert.Equal(0, schedule.AcceptedCount);
        Assert.Equal(TimeSpan.Zero, schedule.UntilNext);
        Assert.Equal(TimeSpan.FromHours(1), schedule.UntilEnd);
    }

    /// <summary>Each of sixty separate minute windows contains one accepted request before completion.</summary>
    [Fact]
    public void RequiresSixtyMinuteWindowsAndFullHour()
    {
        ManualTimeProvider clock = new();
        SoakSchedule schedule = new(clock);
        for (int minute = 0; minute < 60; minute++)
        {
            Assert.True(schedule.NeedsRequest);
            Assert.False(schedule.IsComplete);
            schedule.RecordAccepted();
            Assert.Equal(minute + 1, schedule.AcceptedCount);
            Assert.False(schedule.IsComplete);
            Assert.Equal(TimeSpan.FromMinutes(1), schedule.UntilNext);
            clock.Advance(TimeSpan.FromMinutes(1));
        }
        Assert.False(schedule.NeedsRequest);
        Assert.True(schedule.IsComplete);
        Assert.Equal(TimeSpan.Zero, schedule.UntilEnd);
    }

    /// <summary>Late or duplicate acceptance cannot be used to fill another minute's requirement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMissingMinuteOrEarlyDuplicate(bool duplicate)
    {
        ManualTimeProvider clock = new();
        SoakSchedule schedule = new(clock);
        if (duplicate) { schedule.RecordAccepted(); }
        else { clock.Advance(TimeSpan.FromMinutes(1)); }
        ValidationProbeException failure = Assert.Throws<ValidationProbeException>(schedule.RecordAccepted);
        Assert.Equal("InvalidSoakAcceptanceWindow", failure.Code);
    }
}
