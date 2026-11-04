using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>The setup diagnostic reports a failed default-budget submit once and never replaces or changes its exception.</summary>
public sealed class AdmissionSetupObservationTests
{
    private static readonly TimeSpan _shortBound = TimeSpan.FromMilliseconds(200);

    /// <summary>One line carries the loop index, the candidate and the exception's own fields; the same exception is rethrown.</summary>
    [Fact]
    public async Task ReportsOneLineAndRethrowsTheSameException()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        SessionOperationException failure = Failure(candidate, FailureStage.AdmissionCommit, AdmissionStatus.Unknown);
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start(
            (id, _) => ValueTask.FromResult<RequestSnapshot?>(new(id, Guid.NewGuid(), 8, RequestState.Queued, ExecutionCertainty.NotStarted, null)), lines.Add);
        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 7, candidate, failure));
        Assert.Same(failure, thrown);
        string line = Assert.Single(lines);
        Assert.StartsWith("admission-setup-observation failure index=7 candidate=" + candidate.ToString("D"), line, StringComparison.Ordinal);
        Assert.Contains(" code=AdmissionTimedOut stage=AdmissionCommit status=Unknown certainty=Unknown request=" + candidate.ToString("D"), line, StringComparison.Ordinal);
        Assert.Contains(" lookup=Queued/NotStarted lookup-ms=", line, StringComparison.Ordinal);
        Assert.Contains(" threads=", line, StringComparison.Ordinal);
        Assert.Contains(" gc-pause-delta-ms=", line, StringComparison.Ordinal);
    }

    /// <summary>A failing write is thrown together with the primary failure, which comes first.</summary>
    [Fact]
    public async Task KeepsThePrimaryFirstWhenTheWriteFails()
    {
        Guid candidate = Guid.NewGuid();
        SessionOperationException failure = Failure(candidate, FailureStage.AdmissionLookup, AdmissionStatus.NotAccepted);
        IOException output = new("output unavailable");
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start(
            (_, _) => ValueTask.FromResult<RequestSnapshot?>(null), _ => throw output);
        AggregateException both = await Assert.ThrowsAsync<AggregateException>(() => FailAt(observation, 0, candidate, failure));
        Assert.Same(failure, both.InnerExceptions[0]);
        Assert.Same(output, both.InnerExceptions[1]);
    }

    /// <summary>A lookup that never returns is reported as a timeout within its bound, without joining it.</summary>
    [Fact]
    public async Task BoundsALookupThatNeverReturns()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        TaskCompletionSource<RequestSnapshot?> never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start(
            (_, _) => new ValueTask<RequestSnapshot?>(never.Task), lines.Add, lookupBound: _shortBound);
        long began = Stopwatch.GetTimestamp();
        await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 3, candidate, Failure(candidate, FailureStage.AdmissionCommit, AdmissionStatus.Unknown)))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(5));
        Assert.Contains(" lookup=timeout lookup-ms=", Assert.Single(lines), StringComparison.Ordinal);
        never.SetException(new ObjectDisposedException("store"));
    }

    /// <summary>An absent candidate is reported only as absent at that moment, never as not accepted.</summary>
    [Fact]
    public async Task ReportsAbsenceWithoutInferringNonAcceptance()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start((_, _) => ValueTask.FromResult<RequestSnapshot?>(null), lines.Add);
        await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 1, candidate, Failure(candidate, FailureStage.AdmissionCommit, AdmissionStatus.Unknown)));
        string line = Assert.Single(lines);
        Assert.Contains(" lookup=absent-at-observation lookup-ms=", line, StringComparison.Ordinal);
        Assert.DoesNotContain("NotAccepted", line, StringComparison.Ordinal);
    }

    /// <summary>A lookup failure is reported by its type and does not replace the primary failure.</summary>
    [Fact]
    public async Task ReportsALookupErrorByType()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        SessionOperationException failure = Failure(candidate, FailureStage.AdmissionLookup, AdmissionStatus.NotAccepted);
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start(
            (_, _) => ValueTask.FromException<RequestSnapshot?>(new ObjectDisposedException("store")), lines.Add);
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 2, candidate, failure)));
        Assert.Contains(" lookup=error:System.ObjectDisposedException lookup-ms=", Assert.Single(lines), StringComparison.Ordinal);
    }

    /// <summary>A failure that is not a session operation failure is still reported by type and rethrown unchanged.</summary>
    [Fact]
    public async Task ReportsAnOtherFailureByType()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        TimeoutException failure = new("other");
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start((_, _) => ValueTask.FromResult<RequestSnapshot?>(null), lines.Add);
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 4, candidate, failure)));
        string line = Assert.Single(lines);
        Assert.Contains(" exception=System.TimeoutException code=not-available", line, StringComparison.Ordinal);
    }

    /// <summary>Only the last ten passing submit durations are kept and reported.</summary>
    [Fact]
    public async Task KeepsOnlyTheLastTenDurations()
    {
        Guid candidate = Guid.NewGuid();
        List<string> lines = [];
        await using AdmissionSetupObservation observation = AdmissionSetupObservation.Start((_, _) => ValueTask.FromResult<RequestSnapshot?>(null), lines.Add);
        for (int index = 0; index < 12; index++) { observation.Record(Stopwatch.GetTimestamp()); }
        await Assert.ThrowsAnyAsync<Exception>(() => FailAt(observation, 12, candidate, new TimeoutException()));
        string recent = Assert.Single(lines).Split(" recent-ms=")[1].Split(' ')[0];
        Assert.Equal(10, recent.Split(',').Length);
    }

    /// <summary>The passing path writes nothing.</summary>
    [Fact]
    public async Task WritesNothingOnThePassingPath()
    {
        List<string> lines = [];
        await using (AdmissionSetupObservation observation = AdmissionSetupObservation.Start((_, _) => ValueTask.FromResult<RequestSnapshot?>(null), lines.Add))
        {
            observation.Record(Stopwatch.GetTimestamp());
        }
        Assert.Empty(lines);
    }

    private static async Task FailAt(AdmissionSetupObservation observation, int index, Guid candidate, Exception failure)
    {
        long submitted = Stopwatch.GetTimestamp();
        try { await Task.Yield(); throw failure; }
        catch (Exception primary)
        {
            await observation.ReportAsync(observation.Capture(index, candidate, submitted, primary), primary);
            throw;
        }
    }

    private static SessionOperationException Failure(Guid candidate, FailureStage stage, AdmissionStatus status)
        => new(new PublicError(ErrorCode.AdmissionTimedOut, ExecutionCertainty.Unknown, Guid.NewGuid(), candidate, status)) { Details = new(stage) };
}
