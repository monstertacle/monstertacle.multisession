using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>A scenario failure that is not a typed product error still names its exception category and throw site, never text or paths.</summary>
public sealed class ScenarioFailureAttributionTests
{
    /// <summary>An untyped timeout keeps the generic code, and adds its exception category and the first non-runtime throw site.</summary>
    [Fact]
    public async Task UntypedFailureNamesItsCategoryAndThrowSite()
    {
        TimeoutException timeout = await Assert.ThrowsAsync<TimeoutException>(WaitForProbeBarrierAsync);

        ValidationFailure failure = ValidationFailure.FromException(timeout);
        string json = JsonSerializer.Serialize(failure);

        Assert.Equal(ErrorCode.ExecutionFailed, failure.Error.Code);
        Assert.Equal("TimeoutException", failure.ExceptionType);
        Assert.Equal(nameof(ScenarioFailureAttributionTests) + "." + nameof(WaitForProbeBarrierAsync), failure.Site);
        Assert.DoesNotContain(timeout.Message, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar, json, StringComparison.Ordinal);
    }

    /// <summary>A typed product error keeps its public error and details and adds nothing.</summary>
    [Fact]
    public void TypedFailureKeepsItsPublicErrorOnly()
    {
        SessionOperationException typed = new(new PublicError(ErrorCode.AdmissionTimedOut, RequestId: Guid.NewGuid())) { Details = new(FailureStage.AdmissionCommit) };

        ValidationFailure failure = ValidationFailure.FromException(typed);

        Assert.Equal((typed.Failure, typed.Details), (failure.Error, failure.Details));
        Assert.Null(failure.ExceptionType);
        Assert.Null(failure.Site);
    }

    /// <summary>The runner writes its report to the writer it is given, so an in-process caller can keep a failed report.</summary>
    [Fact]
    public async Task RunnerWritesTheReportToTheSuppliedWriter()
    {
        using StringWriter output = new();

        int exit = await ScenarioRunner.RunAsync(["--scenario", "unknown-scenario", "--state-root", Path.Combine(Path.GetTempPath(), "unused")], output);

        using JsonDocument report = JsonDocument.Parse(output.ToString());
        Assert.Equal(exit, report.RootElement.GetProperty("ExitCode").GetInt32());
        Assert.Equal("FAIL", report.RootElement.GetProperty("Status").GetString());
    }

    private static async Task WaitForProbeBarrierAsync() => await Task.Delay(Timeout.InfiniteTimeSpan).WaitAsync(TimeSpan.FromMilliseconds(1));
}
