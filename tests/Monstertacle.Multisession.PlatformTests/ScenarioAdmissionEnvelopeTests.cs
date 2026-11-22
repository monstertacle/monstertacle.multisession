using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.PlatformTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>
/// SDK-PR41-WIN-RETENTION-ADMISSION-001 (M1 extended to ScenarioRunner admissions, test/ValidationHost only): a failed scenario's
/// report names its admissions in order, with monotonic start and duration and the failure's own known facts, while its original
/// classification is unchanged; a passing report carries none. Nothing is looked up, retried or reclassified.
/// </summary>
public sealed class ScenarioAdmissionEnvelopeTests
{
    // The real lease is released first; then the disposal reports a failure (a state cleanup failure after a released lease).
    private sealed class CleanupFailingLease(IStateDirectoryLease inner) : IStateDirectoryLease
    {
        public bool IsHeld => inner.IsHeld;
        public string StateDirectory => inner.StateDirectory;
        public bool Matches(string stateDirectory) => inner.Matches(stateDirectory);
        public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken) =>
            inner.PrepareFileAsync(fileName, mode, cancellationToken);
        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            throw new IOException("state cleanup failed after the lease was released");
        }
    }

    private static async Task<ValidationReport> RunAsync(string scenario, bool fail, bool failCleanup = false)
    {
        OwnedScenarioRoot owned = OwnedScenarioRoot.UnderScratch("multisession-admission-envelope-");
        try
        {
            ValidationRunState created = await ValidationStateRoot.CreateRunAsync(owned.Path, CancellationToken.None);
            // The body always runs on the real run state; only the state the runner disposes reports a cleanup failure.
            ValidationRunState state = failCleanup ? new(created.RunId, new CleanupFailingLease(created.Lease), created.TrustScope) : created;
            ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync(scenario, state, async _ =>
            {
                ValidationRunState current = created;
                ProbeProvider provider = new("probe", new ProbeTrace(), (call, binding, token) => ValueTask.FromResult(ProbeReplies.Success(call, binding)));
                await using ProbeHost host = await ProbeHost.StartAsync(current, [new("probe", "probe", 1, provider)]);
                SessionSnapshot session = await host.CreateAsync("probe");
                RequestReceipt first = await host.SubmitAsync(session.SessionId, "accepted-first");
                await host.EndAsync(first.RequestId);
                if (fail) { await host.SubmitAsync(Guid.NewGuid(), "unknown-session"); }
                await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            });
            owned.Passed();
            return report;
        }
        finally { owned.Finish(); }
    }

    /// <summary>A failed admission is reported after the accepted one, with its own facts, and the report keeps its classification.</summary>
    [Fact]
    public async Task ReportsTheAdmissionsOfAFailedScenarioWithItsOriginalClassification()
    {
        ValidationReport report = await RunAsync("admission-envelope-failure", fail: true);
        Assert.Equal("FAIL", report.Status);
        Assert.NotNull(report.Failure);
        Assert.Equal(report.Failure!.Error.Code.ToString(), report.Reason);
        using JsonDocument written = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.True(written.RootElement.TryGetProperty("Admissions", out JsonElement admissions), "the failed report names its admissions");
        JsonElement[] items = [.. admissions.EnumerateArray()];
        Assert.Equal(2, items.Length);
        Assert.Equal(["submit:accepted-first", "submit:unknown-session"], items.Select(item => item.GetProperty("Phase").GetString()));
        Assert.Equal(["accepted", "failed"], items.Select(item => item.GetProperty("Outcome").GetString()));
        Assert.Equal(report.Failure.Error.Code.ToString(), items[1].GetProperty("Code").GetString());
        Assert.Equal(report.Failure.Details?.Stage.ToString() ?? "not-available", items[1].GetProperty("Stage").GetString());
        Assert.Equal(report.Failure.Error.Certainty.ToString(), items[1].GetProperty("Certainty").GetString());
        Assert.True(items[1].GetProperty("StartMs").GetDouble() >= items[0].GetProperty("StartMs").GetDouble() + items[0].GetProperty("DurationMs").GetDouble());
    }

    /// <summary>
    /// SDK-SCENARIO-CLEANUP-ENVELOPE-001: a scenario whose admissions succeeded but whose state cleanup failed ends as a failed report
    /// with its own admission envelope; no primary failure is invented.
    /// </summary>
    [Fact]
    public async Task KeepsTheAdmissionsOfAScenarioWhoseOnlyFailureIsCleanup()
    {
        ValidationReport report = await RunAsync("admission-envelope-cleanup", fail: false, failCleanup: true);
        Assert.Equal("FAIL", report.Status);
        Assert.NotEqual(0, report.ExitCode);
        Assert.Equal("ScenarioCleanupFailed", report.Reason);
        Assert.Null(report.Failure);
        Assert.Single(report.CleanupFailures);
        using JsonDocument written = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.True(written.RootElement.TryGetProperty("Admissions", out JsonElement admissions), "the cleanup-only failed report names its admissions");
        JsonElement only = Assert.Single(admissions.EnumerateArray());
        Assert.Equal("submit:accepted-first", only.GetProperty("Phase").GetString());
        Assert.Equal("accepted", only.GetProperty("Outcome").GetString());
    }

    /// <summary>A body failure stays primary when cleanup also fails; cleanup is attached and the envelope lists both admissions.</summary>
    [Fact]
    public async Task KeepsTheBodyFailurePrimaryWhenCleanupAlsoFails()
    {
        ValidationReport report = await RunAsync("admission-envelope-both", fail: true, failCleanup: true);
        Assert.Equal("FAIL", report.Status);
        Assert.NotNull(report.Failure);
        Assert.Equal(report.Failure!.Error.Code.ToString(), report.Reason);
        Assert.Single(report.CleanupFailures);
        using JsonDocument written = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.Equal(["accepted", "failed"], written.RootElement.GetProperty("Admissions").EnumerateArray().Select(item => item.GetProperty("Outcome").GetString()));
    }

    /// <summary>A passing report is unchanged: it carries no admission envelope.</summary>
    [Fact]
    public async Task APassingScenarioReportCarriesNoAdmissions()
    {
        ValidationReport report = await RunAsync("admission-envelope-pass", fail: false);
        Assert.Equal("PASS", report.Status);
        using JsonDocument written = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.False(written.RootElement.TryGetProperty("Admissions", out _));
    }
}
