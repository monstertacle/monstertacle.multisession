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
    [InlineData("capacity", 10, 5, 5, 0, 0)]
    [InlineData("workspace", 3, 3, 3, 0, 0)]
    [InlineData("limits", 1, 3, 2, 1, 0)]
    [InlineData("isolation", 1, 1, 1, 0, 0)]
    [InlineData("extensions", 2, 3, 3, 0, 0)]
    [InlineData("native-context", 3, 5, 4, 1, 0)]
    public async Task ExecutesSelectedWorkload(string scenario, int sessions, int requests, int succeeded, int failed, int cancelled)
    {
        await using PrivateStoreDirectory parent = new();
        string root = Path.Combine(parent.StateDirectory, "scenario-runs");
        string database = await RunPassedAsync(scenario, root);
        Assert.Equal((long)sessions, StoreProbe.Scalar(database, "SELECT count(*) FROM Sessions"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM History"));
        Assert.Equal((long)succeeded, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=2"));
        Assert.Equal((long)failed, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=3"));
        Assert.Equal((long)cancelled, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=4"));
        Assert.Equal(0L, StoreProbe.Scalar(database, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Shutdown and retention preserve uncertain external work and its reservation after late provider completion.</summary>
    [Theory]
    [InlineData("shutdown", 3, 2)]
    [InlineData("retention", 1, 0)]
    public async Task KeepsUnknownReservationAndCancelsOnlyNeverStartedWork(string scenario, int requests, int neverStarted)
    {
        await using PrivateStoreDirectory parent = new();
        string database = await RunPassedAsync(scenario, Path.Combine(parent.StateDirectory, "uncertain-runs"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests"));
        Assert.Equal((long)requests, StoreProbe.Scalar(database, "SELECT count(*) FROM History"));
        Assert.Equal(0L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State IN (0,1,2)"));
        Assert.Equal((long)neverStarted, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=4 AND Certainty=0"));
        Assert.Equal(1L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE Certainty=3"));
        Assert.Equal(1L, StoreProbe.Scalar(database, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Ten native process kills preserve the complete SC-009 dataset in independently reopened stores.</summary>
    [Fact]
    public async Task RecoveryExecutesTenNativeRestarts()
    {
        await using PrivateStoreDirectory parent = new();
        string run = await RunPassedAsync("recovery", Path.Combine(parent.StateDirectory, "recovery-runs"), requireDatabase: false);
        string[] stores = Directory.GetDirectories(run, "multisession-recovery-*");
        Assert.Equal(10, stores.Length);
        foreach (string store in stores)
        {
            string database = Path.Combine(store, "multisession.db");
            Assert.Equal(2L, StoreProbe.Scalar(database, "SELECT count(*) FROM Sessions"));
            Assert.Equal(6L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests"));
            Assert.Equal(6L, StoreProbe.Scalar(database, "SELECT count(*) FROM History"));
            Assert.Equal(5L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=2"));
            Assert.Equal(0L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State IN (0,1)"));
            Assert.Equal(1L, StoreProbe.Scalar(database, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        }
    }

    private static async Task<string> RunPassedAsync(string scenario, string root, bool requireDatabase = true)
    {
        ProcessStartInfo start = new("dotnet");
        foreach (string argument in new[] { ValidationHostBuild.WorkerPath(), "--scenario", scenario, "--state-root", root })
        {
            start.ArgumentList.Add(argument);
        }
        ProbeProcessResult result = await ProbeProcess.RunAsync(start, scenario is "recovery" or "routing" ? TimeSpan.FromMinutes(5) : null);
        Assert.True(result.ExitCode == 0, $"Scenario exited {result.ExitCode}: {result.Output}");
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("PASS", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal(scenario, report.RootElement.GetProperty("Scenario").GetString());
        string database = Path.Combine(Assert.Single(Directory.GetDirectories(root)), "multisession.db");
        if (!requireDatabase) { return Path.GetDirectoryName(database)!; }
        Assert.True(File.Exists(database));
        return database;
    }
}
