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
    private static async Task<ValidationReport> RunAsync(string scenario, bool fail)
    {
        OwnedScenarioRoot owned = OwnedScenarioRoot.UnderScratch("multisession-admission-envelope-");
        try
        {
            ValidationRunState state = await ValidationStateRoot.CreateRunAsync(owned.Path, CancellationToken.None);
            ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync(scenario, state, async current =>
            {
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
