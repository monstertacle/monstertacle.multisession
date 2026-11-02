using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>A failed crash worker names the stage, admission status, candidate and step of its failure, never paths or text.</summary>
public sealed class CrashFailureReportTests
{
    /// <summary>A timed-out admission reports its stage, unknown acceptance, candidate identity, failing step and earlier step times.</summary>
    [Fact]
    public async Task AttributesATimedOutAdmission()
    {
        Guid candidate = Guid.NewGuid();
        CrashStepClock clock = new();
        await clock.MeasureAsync("store-open", () => Task.CompletedTask);
        SessionOperationException timedOut = new(new PublicError(ErrorCode.AdmissionTimedOut, ExecutionCertainty.Unknown, RequestId: candidate,
            AdmissionStatus: AdmissionStatus.Unknown))
        { Details = new(FailureStage.AdmissionCommit) };

        SessionOperationException thrown = await Assert.ThrowsAsync<SessionOperationException>(() => clock.MeasureAsync("queued-a-1", () => throw timedOut));
        CrashFailure report = JsonSerializer.Deserialize<CrashFailure>(JsonSerializer.Serialize(CrashFailure.From(thrown, clock)))!;

        Assert.Equal(("failed", "AdmissionTimedOut", "AdmissionCommit", "Unknown"), (report.Status, report.FailureCode, report.Stage, report.AdmissionStatus));
        Assert.Equal(candidate, report.RequestId);
        Assert.Equal("queued-a-1", report.Step);
        Assert.Equal(["store-open"], report.Steps.Select(step => step.Name));
    }

    /// <summary>An unexpected failure keeps only its finite category and the failing step.</summary>
    [Fact]
    public async Task KeepsOnlyTheCategoryOfAnUnexpectedFailure()
    {
        CrashStepClock clock = new();
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            clock.MeasureAsync("dispatch-running", () => throw new InvalidOperationException("secret native text")));

        CrashFailure report = CrashFailure.From(thrown, clock);

        Assert.Equal(("crash-fixture", "dispatch-running"), (report.FailureCode, report.Step));
        Assert.Null(report.Stage);
        Assert.Null(report.RequestId);
        Assert.DoesNotContain("secret native text", JsonSerializer.Serialize(report), StringComparison.Ordinal);
    }
}
