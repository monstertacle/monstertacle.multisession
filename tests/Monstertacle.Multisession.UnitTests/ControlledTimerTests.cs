using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Advancing the test clock must drive actual BCL deadlines, not merely displayed timestamps.</summary>
public sealed class ControlledTimerTests
{
    /// <summary>The deadline boundary is determined by controlled elapsed time without waiting an hour.</summary>
    [Theory]
    [InlineData(3599999, false)]
    [InlineData(3600000, true)]
    public void AdvancesCancellationTimerAtExactBoundary(int milliseconds, bool cancelled)
    {
        ManualTimeProvider clock = new();
        using CancellationTokenSource deadline = new(TimeSpan.FromHours(1), clock);
        long start = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Assert.Equal(cancelled, deadline.IsCancellationRequested);
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), clock.GetElapsedTime(start));
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(milliseconds), clock.GetUtcNow());
    }

    /// <summary>Task.Delay completes from a fake timer and releases its timer on caller cancellation.</summary>
    [Fact]
    public void AdvancesTaskDelayWithoutWallClockWaiting()
    {
        ManualTimeProvider clock = new();
        using CancellationTokenSource cancellation = new();
        Task delay = Task.Delay(TimeSpan.FromHours(1), clock, cancellation.Token);
        try
        {
            clock.Advance(TimeSpan.FromHours(1));
            Assert.True(delay.IsCompletedSuccessfully);
        }
        finally { cancellation.Cancel(); }
    }
}
