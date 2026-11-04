using System.Diagnostics;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Stage observation reports the last observed stages once after the measured work, whether the work passed or failed.</summary>
public sealed class StageObservationTests
{
    private const int _first = 0;
    private const int _second = 1;

    /// <summary>A passing window is written once, after the work, with every observed stage.</summary>
    [Fact]
    public async Task WritesOnceAfterThePassingWindow()
    {
        StageObservation stages = new("T000", "first", "second");
        List<string> lines = [];
        bool workEnded = false;
        await stages.ObserveAsync(Stopwatch.GetTimestamp(), () =>
        {
            stages.Mark(_first);
            stages.Mark(_second);
            Assert.Empty(lines);
            workEnded = true;
            return Task.CompletedTask;
        }, line => { Assert.True(workEnded); lines.Add(line); });
        string written = Assert.Single(lines);
        Assert.StartsWith("stage-observation T000 outcome=passed first-ms=", written, StringComparison.Ordinal);
        Assert.Contains(" second-ms=", written, StringComparison.Ordinal);
        Assert.DoesNotContain("not-observed", written, StringComparison.Ordinal);
    }

    /// <summary>A failure before a stage is reached is written with the failure type and the stage marked as not observed.</summary>
    [Fact]
    public async Task WritesEarlyFailuresWithUnobservedStages()
    {
        StageObservation stages = new("T000", "first", "second");
        List<string> lines = [];
        InvalidOperationException primary = new("measured work failed");
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => stages.ObserveAsync(Stopwatch.GetTimestamp(), () =>
        {
            stages.Mark(_first);
            throw primary;
        }, lines.Add));
        Assert.Same(primary, thrown);
        string written = Assert.Single(lines);
        Assert.StartsWith("stage-observation T000 outcome=failed:System.InvalidOperationException first-ms=", written, StringComparison.Ordinal);
        Assert.EndsWith(" second=not-observed", written, StringComparison.Ordinal);
    }

    /// <summary>A stamp recorded elsewhere counts only when it was observed.</summary>
    [Fact]
    public async Task ReportsAnUnsetExternalStampAsNotObserved()
    {
        StageObservation stages = new("T000", "first", "second");
        List<string> lines = [];
        await stages.ObserveAsync(Stopwatch.GetTimestamp(), () => Task.CompletedTask, lines.Add,
            () => { stages.Record(_first, Stopwatch.GetTimestamp()); stages.Record(_second, 0); });
        string written = Assert.Single(lines);
        Assert.Contains(" first-ms=", written, StringComparison.Ordinal);
        Assert.EndsWith(" second=not-observed", written, StringComparison.Ordinal);
    }

    /// <summary>A failing write after a failed window is reported together with the original failure, which comes first.</summary>
    [Fact]
    public async Task KeepsThePrimaryFailureWhenTheWriteFails()
    {
        StageObservation stages = new("T000", "first");
        InvalidOperationException primary = new("measured work failed");
        IOException output = new("output unavailable");
        AggregateException both = await Assert.ThrowsAsync<AggregateException>(() => stages.ObserveAsync(Stopwatch.GetTimestamp(),
            () => throw primary, _ => throw output));
        Assert.Same(primary, both.InnerExceptions[0]);
        Assert.Same(output, both.InnerExceptions[1]);
    }

    /// <summary>A failing write after a passing window fails the test with the write failure.</summary>
    [Fact]
    public async Task ReportsAWriteFailureAfterAPassingWindow()
    {
        StageObservation stages = new("T000", "first");
        IOException output = new("output unavailable");
        IOException thrown = await Assert.ThrowsAsync<IOException>(() => stages.ObserveAsync(Stopwatch.GetTimestamp(),
            () => Task.CompletedTask, _ => throw output));
        Assert.Same(output, thrown);
    }
}
