using System.Diagnostics;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>The scheduling sampler reports readiness, samples and lag honestly and stops within a bound.</summary>
public sealed class SchedulingSamplerTests
{
    private static readonly TimeSpan _shortBound = TimeSpan.FromMilliseconds(200);


    /// <summary>A sampler that has not started reports that explicitly, never a zero lag, and a stuck stop is bounded and visible.</summary>
    [Fact]
    public async Task ReportsAnUnstartedSamplerAsUnknown()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SchedulingSampler sampler = new(startGate: gate.Task);
        string snapshot = Read(sampler);
        Assert.Contains("sampler-ready=not-ready samples=0 max-lag-ms=not-sampled end-gap-ms=unknown", snapshot, StringComparison.Ordinal);
        Assert.Equal("stop-timeout", await sampler.StopAsync(_shortBound).WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sampler.DisposeAsync().AsTask());
        gate.SetResult();
    }

    /// <summary>A ready sampler without a completed sample reports no lag, not zero lag.</summary>
    [Fact]
    public async Task ReportsAReadySamplerWithoutSamples()
    {
        TaskCompletionSource never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using SchedulingSampler sampler = new((_, token) => never.Task.WaitAsync(token));
        await WaitUntilAsync(() => Read(sampler).Contains("sampler-ready=", StringComparison.Ordinal)
            && !Read(sampler).Contains("not-ready", StringComparison.Ordinal));
        string snapshot = Read(sampler);
        Assert.Contains(" samples=0 max-lag-ms=not-sampled end-gap-ms=", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("end-gap-ms=unknown", snapshot, StringComparison.Ordinal);
    }

    /// <summary>A late sample is reported as lag.</summary>
    [Fact]
    public async Task ReportsADelayedSampleAsLag()
    {
        int calls = 0;
        await using SchedulingSampler sampler = new((period, token) => Task.Delay(Interlocked.Increment(ref calls) == 1 ? TimeSpan.FromMilliseconds(400) : period, token));
        await WaitUntilAsync(() => !Read(sampler).Contains(" samples=0 ", StringComparison.Ordinal));
        string snapshot = Read(sampler);
        double lag = double.Parse(snapshot.Split("max-lag-ms=")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(lag >= 300, snapshot);
        Assert.Equal("stopped", await sampler.StopAsync(TimeSpan.FromSeconds(2)));
    }

    /// <summary>A sample published after the failure point is never reported as if it preceded that point.</summary>
    [Fact]
    public async Task NeverReportsASampleNewerThanTheFailurePointAsEarlier()
    {
        await using SchedulingSampler sampler = new();
        long failureAt = Stopwatch.GetTimestamp();
        await WaitUntilAsync(() => sampler.Snapshot().LastSample is { } last && last > failureAt);
        string snapshot = sampler.Snapshot().Describe(failureAt);
        Assert.DoesNotContain("end-gap-ms=-", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("sampler-observed-ms-after-failure=-", snapshot, StringComparison.Ordinal);
        Assert.Contains(" last-sample-after-failure=yes", snapshot, StringComparison.Ordinal);
    }

    // Reads the sampler as the fixture does: the failure point first, then the snapshot.
    private static string Read(SchedulingSampler sampler)
    {
        long failureAt = Stopwatch.GetTimestamp();
        return sampler.Snapshot().Describe(failureAt);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        long began = Stopwatch.GetTimestamp();
        while (!condition())
        {
            Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(5), "condition not reached");
            await Task.Delay(10);
        }
    }
}
