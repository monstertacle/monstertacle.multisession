using System.Diagnostics;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>The scheduling sampler is finite, freezes one immutable window, labels its values honestly and never hides the measured failure.</summary>
public sealed class SchedulingSamplerTests
{
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(5);

    /// <summary>A held sampler thread cannot hold the freeze: the join is bounded and its late write is discarded.</summary>
    [Fact]
    public async Task BoundsTheFreezeWhileTheSamplerThreadIsHeld()
    {
        TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        SchedulingSampler sampler = new(onWake: () => { held.TrySetResult(); release.Wait(_guard); });
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        await held.Task.WaitAsync(_guard);
        long freezing = Stopwatch.GetTimestamp();
        SamplerSnapshot snapshot = sampler.Freeze(origin, Stopwatch.GetTimestamp());
        Assert.True(Stopwatch.GetElapsedTime(freezing) < TimeSpan.FromSeconds(2), "the freeze waited for the held thread");
        Assert.False(snapshot.Joined);
        string text = snapshot.Format();
        Assert.Contains(" stop=timeout ", text, StringComparison.Ordinal);
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => !sampler.IsRunning, _guard), "the released thread did not end");
        Assert.Equal(1, sampler.LateDiscarded);
        Assert.Same(snapshot, sampler.Freeze(origin, Stopwatch.GetTimestamp()));
        Assert.Equal(text, snapshot.Format());
    }

    /// <summary>At most one pool probe is outstanding; a held probe is unresolved for the window, not a dispatch maximum.</summary>
    [Fact]
    public async Task KeepsAtMostOneOutstandingPoolProbe()
    {
        TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        SchedulingSampler sampler = new(onProbe: () => { held.TrySetResult(); release.Wait(_guard); });
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        await held.Task.WaitAsync(_guard);
        Assert.True(SpinWait.SpinUntil(() => sampler.Skipped >= 3, _guard), "wakes did not skip while the probe was outstanding");
        SamplerSnapshot snapshot = sampler.Freeze(origin, Stopwatch.GetTimestamp());
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => sampler.LateDiscarded == 1, _guard), "the held probe did not end");
        Assert.Equal(1, sampler.MaxOutstanding);
        Assert.True(snapshot.PoolUnresolvedForWindow);
        Assert.True(snapshot.PoolUnresolvedAgeInWindowMs > 0);
        Assert.Equal(0, snapshot.PoolCount);
        Assert.Null(snapshot.PoolDispatchMaxMs);
        Assert.True(snapshot.PoolSkipped >= 3);
        Assert.Contains(" pool-dispatch-max-ms=missing pool-unresolved-for-window=true pool-unresolved-age-in-window-ms=", snapshot.Format(), StringComparison.Ordinal);
    }

    /// <summary>A probe queued before the origin and run inside the window is selected by its run stamp with its full latency.</summary>
    [Fact]
    public async Task SelectsAProbeQueuedBeforeTheOriginWithItsFullLatency()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        long enteredAt = 0;
        int calls = 0;
        SchedulingSampler sampler = new(onProbeStart: () =>
        {
            if (Interlocked.Increment(ref calls) != 1) { return; }
            enteredAt = Stopwatch.GetTimestamp();
            entered.TrySetResult();
            release.Wait(_guard);
        });
        sampler.Start();
        await entered.Task.WaitAsync(_guard);
        await Task.Delay(50);
        long origin = Stopwatch.GetTimestamp();
        await Task.Delay(50);
        long released = Stopwatch.GetTimestamp();
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => sampler.Probes >= 1, _guard), "the released probe was not recorded");
        SamplerSnapshot snapshot = sampler.Freeze(origin, Stopwatch.GetTimestamp());
        Assert.True(snapshot.PoolCount >= 1);
        double queuedToRelease = Stopwatch.GetElapsedTime(enteredAt, released).TotalMilliseconds;
        Assert.True(snapshot.PoolDispatchMaxMs >= queuedToRelease, $"latency {snapshot.PoolDispatchMaxMs} ms was clipped below {queuedToRelease} ms");
    }

    /// <summary>A probe whose callback started but was not recorded by the freeze is unresolved, with its age clipped to the origin.</summary>
    [Fact]
    public async Task ReportsAStartedButUnrecordedProbeAsUnresolvedWithAClippedAge()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        int calls = 0;
        SchedulingSampler sampler = new(onProbe: () =>
        {
            if (Interlocked.Increment(ref calls) != 1) { return; }
            started.TrySetResult();
            release.Wait(_guard);
        });
        sampler.Start();
        await started.Task.WaitAsync(_guard);
        await Task.Delay(50);
        long origin = Stopwatch.GetTimestamp();
        await Task.Delay(20);
        long end = Stopwatch.GetTimestamp();
        SamplerSnapshot snapshot = sampler.Freeze(origin, end);
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => sampler.LateDiscarded == 1, _guard), "the started probe did not end");
        Assert.True(snapshot.PoolUnresolvedForWindow, "a started but unrecorded probe must stay unresolved");
        Assert.Equal(0, snapshot.PoolCount);
        double window = Stopwatch.GetElapsedTime(origin, end).TotalMilliseconds;
        Assert.True(snapshot.PoolUnresolvedAgeInWindowMs <= window, $"unresolved age {snapshot.PoolUnresolvedAgeInWindowMs} ms exceeds the {window} ms window");
    }

    /// <summary>A probe that runs after the freeze is discarded and cannot change the emitted snapshot.</summary>
    [Fact]
    public async Task DiscardsAProbeReleasedAfterTheFreeze()
    {
        TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        SchedulingSampler sampler = new(onProbe: () => { held.TrySetResult(); release.Wait(_guard); });
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        await held.Task.WaitAsync(_guard);
        SamplerSnapshot snapshot = sampler.Freeze(origin, Stopwatch.GetTimestamp());
        SamplerSnapshot copy = snapshot with { };
        string text = snapshot.Format();
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => sampler.LateDiscarded == 1, _guard), "the late probe was not discarded");
        Assert.Equal(copy, snapshot);
        Assert.Equal(text, snapshot.Format());
        Assert.Same(snapshot, sampler.Freeze(origin, Stopwatch.GetTimestamp()));
    }

    /// <summary>Samples outside the window never count; an empty window is missing, and its age is labelled as since the origin.</summary>
    [Fact]
    public void ReportsAnEmptyWindowAsMissing()
    {
        SchedulingSampler sampler = new();
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        Assert.True(SpinWait.SpinUntil(() => sampler.Wakes >= 3, _guard), "the sampler did not wake");
        SamplerSnapshot snapshot = sampler.Freeze(origin, origin);
        Assert.Equal(0, snapshot.DedicatedCount);
        Assert.Null(snapshot.DedicatedOversleepMaxMs);
        Assert.Equal(0, snapshot.PoolCount);
        Assert.Null(snapshot.PoolDispatchMaxMs);
        string text = snapshot.Format();
        Assert.Contains(" dedicated-n=0 dedicated-oversleep-max-ms=missing dedicated-age-since-origin-ms=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dedicated-last-wake-age-ms", text, StringComparison.Ordinal);
        Assert.Contains(" pool-n=0 pool-dispatch-max-ms=missing ", text, StringComparison.Ordinal);
    }

    /// <summary>An unobserved end stage ends the window at the freeze and says so; selected wakes give a last-wake age.</summary>
    [Fact]
    public void MarksAnUnobservedEndAsFreeze()
    {
        SchedulingSampler sampler = new();
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        Assert.True(SpinWait.SpinUntil(() => sampler.Wakes >= 2, _guard), "the sampler did not wake");
        SamplerSnapshot snapshot = sampler.Freeze(origin, 0);
        Assert.True(snapshot.EndAtFreeze);
        Assert.True(snapshot.Joined);
        string text = snapshot.Format();
        Assert.StartsWith("sampler=complete window-end=freeze stop=joined ", text, StringComparison.Ordinal);
        Assert.Contains(" dedicated-last-wake-age-ms=", text, StringComparison.Ordinal);
        Assert.True(snapshot.DedicatedCount >= 2);
    }

    /// <summary>A sampler fault is part of the line; the measured failure stays first, also when the write then fails.</summary>
    [Fact]
    public async Task KeepsThePrimaryFailureWhenTheSamplerFaults()
    {
        TaskCompletionSource faulted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SchedulingSampler sampler = new(onWake: () => { faulted.TrySetResult(); throw new InvalidOperationException("private sampler fault"); });
        StageObservation stages = new("T000", "end");
        InvalidOperationException primary = new("measured work failed");
        IOException output = new("output unavailable");
        List<string> lines = [];
        sampler.Start();
        long origin = Stopwatch.GetTimestamp();
        AggregateException both = await Assert.ThrowsAsync<AggregateException>(() => stages.ObserveAsync(origin, async () =>
        {
            await faulted.Task.WaitAsync(_guard);
            Assert.True(SpinWait.SpinUntil(() => !sampler.IsRunning, _guard), "the faulted thread did not end");
            throw primary;
        }, line => { lines.Add(line); throw output; }, () => stages.Append(sampler.Freeze(origin, stages.Stamp(0)).Format())));
        Assert.Same(primary, both.InnerExceptions[0]);
        Assert.Same(output, both.InnerExceptions[1]);
        string written = Assert.Single(lines);
        Assert.StartsWith("stage-observation T000 outcome=failed:System.InvalidOperationException end=not-observed sampler=fault:System.InvalidOperationException window-end=freeze", written, StringComparison.Ordinal);
    }

    /// <summary>A sampler that is never frozen ends by itself at its lifetime cap.</summary>
    [Fact]
    public void EndsAtItsLifetimeCapWithoutAFreeze()
    {
        SchedulingSampler sampler = new(lifetime: TimeSpan.FromMilliseconds(300));
        long origin = Stopwatch.GetTimestamp();
        sampler.Start();
        Assert.True(SpinWait.SpinUntil(() => !sampler.IsRunning, _guard), "the sampler outlived its cap");
        Assert.StartsWith("sampler=expired ", sampler.Freeze(origin, 0).Format(), StringComparison.Ordinal);
    }
}
