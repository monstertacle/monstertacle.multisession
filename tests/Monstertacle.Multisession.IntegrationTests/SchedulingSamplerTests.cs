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
        string snapshot = sampler.Snapshot(Stopwatch.GetTimestamp()).Describe();
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
        await WaitUntilAsync(() => sampler.Snapshot(Stopwatch.GetTimestamp()).Describe().Contains("sampler-ready=", StringComparison.Ordinal)
            && !sampler.Snapshot(Stopwatch.GetTimestamp()).Describe().Contains("not-ready", StringComparison.Ordinal));
        string snapshot = sampler.Snapshot(Stopwatch.GetTimestamp()).Describe();
        Assert.Contains(" samples=0 max-lag-ms=not-sampled end-gap-ms=", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("end-gap-ms=unknown", snapshot, StringComparison.Ordinal);
    }

    /// <summary>A late sample is reported as lag.</summary>
    [Fact]
    public async Task ReportsADelayedSampleAsLag()
    {
        int calls = 0;
        await using SchedulingSampler sampler = new((period, token) => Task.Delay(Interlocked.Increment(ref calls) == 1 ? TimeSpan.FromMilliseconds(400) : period, token));
        await WaitUntilAsync(() => !sampler.Snapshot(Stopwatch.GetTimestamp()).Describe().Contains(" samples=0 ", StringComparison.Ordinal));
        string snapshot = sampler.Snapshot(Stopwatch.GetTimestamp()).Describe();
        double lag = double.Parse(snapshot.Split("max-lag-ms=")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(lag >= 300, snapshot);
        Assert.Equal("stopped", await sampler.StopAsync(TimeSpan.FromSeconds(2)));
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
