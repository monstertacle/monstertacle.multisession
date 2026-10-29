using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Scenario success is independently checked against the actual persisted dataset.</summary>
public sealed class ValidationScenarioTests
{
    /// <summary>Documented deterministic scenarios execute their real workload and persist every accepted terminal result.</summary>
    [Theory]
    [InlineData("deterministic", 10, 20, 20, 0, 0)]
    [InlineData("routing", 10, 100, 100, 0, 0)]
    [InlineData("failures", 10, 10, 5, 5, 0)]
    [InlineData("cancellation", 1, 1, 0, 0, 1)]
    public async Task ExecutesSelectedWorkload(string scenario, int sessions, int requests, int succeeded, int failed, int cancelled)
    {
        await using PrivateStoreDirectory parent = new();
        string root = Path.Combine(parent.StateDirectory, "scenario-runs");
        ProcessStartInfo start = new("dotnet");
        foreach (string argument in new[] { ValidationHostBuild.WorkerPath(), "--scenario", scenario, "--state-root", root })
        {
            start.ArgumentList.Add(argument);
        }
        ProbeProcessResult result = await ProbeProcess.RunAsync(start);
        Assert.Equal(0, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("PASS", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal(scenario, report.RootElement.GetProperty("Scenario").GetString());
        string database = Path.Combine(Assert.Single(Directory.GetDirectories(root)), "multisession.db");
        Assert.True(File.Exists(database));
        Assert.Equal((long)sessions, StoreProbe.Scalar(database, "SELECT count(*) FROM Sessions"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM History"));
        Assert.Equal((long)succeeded, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=2"));
        Assert.Equal((long)failed, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=3"));
        Assert.Equal((long)cancelled, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=4"));
        Assert.Equal(0L, StoreProbe.Scalar(database, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
