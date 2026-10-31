using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Public scenarios create their own private state without adopting or replacing foreign contents.</summary>
public sealed class ValidationStateRootTests
{
    /// <summary>A populated unmarked directory is left byte-for-byte intact, without even adding a lock or marker.</summary>
    [Fact]
    public async Task RefusesPopulatedForeignRootWithoutAddingOrChangingFiles()
    {
        await using PrivateStoreDirectory directory = new();
        await directory.PrepareFileAsync("foreign-state", StateFileMode.CreateNew, CancellationToken.None);
        string file = Path.Combine(directory.StateDirectory, "foreign-state");
        await File.WriteAllTextAsync(file, "preserve unrelated application state");
        string[] entries = Directory.GetFileSystemEntries(directory.StateDirectory);
        ProbeProcessResult result = await RunAsync(directory.StateDirectory);
        Assert.Equal(2, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("FAIL", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("UnownedStateRoot", report.RootElement.GetProperty("Reason").GetString());
        Assert.Equal(entries, Directory.GetFileSystemEntries(directory.StateDirectory));
        Assert.Equal("preserve unrelated application state", await File.ReadAllTextAsync(file));
    }

    /// <summary>A file cannot be silently replaced by a validation directory.</summary>
    [Fact]
    public async Task RefusesFileAsStateRootWithoutReplacingIt()
    {
        await using PrivateStoreDirectory directory = new();
        string file = Path.Combine(directory.StateDirectory, "existing-file");
        await File.WriteAllTextAsync(file, "preserve file");
        ProbeProcessResult result = await RunAsync(file);
        Assert.Equal(2, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("InvalidStateRoot", report.RootElement.GetProperty("Reason").GetString());
        Assert.Equal("preserve file", await File.ReadAllTextAsync(file));
    }

    /// <summary>Each completed public embedding probe has isolated persistent records and preserves the earlier run.</summary>
    [Fact]
    public async Task RunsConsumerTwiceInDistinctPrivateChildrenWithoutChangingEarlierData()
    {
        await using PrivateStoreDirectory parent = new();
        string root = Path.Combine(parent.StateDirectory, "validation-runs");
        Guid first = await PassedRunAsync(root);
        string firstDirectory = Assert.Single(Directory.GetDirectories(root));
        Dictionary<string, string> original = Snapshot(firstDirectory);
        Guid second = await PassedRunAsync(root);
        Assert.NotEqual(first, second);
        Assert.Equal(2, Directory.GetDirectories(root).Length);
        Assert.Equal(original.OrderBy(item => item.Key), Snapshot(firstDirectory).OrderBy(item => item.Key));
        foreach (string run in Directory.GetDirectories(root))
        {
            string database = Path.Combine(run, "multisession.db");
            Assert.True(File.Exists(database));
            Assert.Equal(5L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests"));
            Assert.Equal(2L, StoreProbe.Scalar(database, "SELECT count(*) FROM Sessions"));
            Assert.Equal(5L, StoreProbe.Scalar(database, "SELECT count(*) FROM History"));
            Assert.Equal(4L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=2"));
            Assert.Equal(1L, StoreProbe.Scalar(database, "SELECT count(*) FROM Requests WHERE State=4"));
            Assert.Equal(0L, StoreProbe.Scalar(database, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        }
    }

    private static async Task<Guid> PassedRunAsync(string root)
    {
        ProbeProcessResult result = await RunAsync(root);
        Assert.Equal(0, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("PASS", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("embedded-consumer", report.RootElement.GetProperty("Scenario").GetString());
        return report.RootElement.GetProperty("RunId").GetGuid();
    }
    private static Dictionary<string, string> Snapshot(string directory)
        => Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(directory, file), file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), StringComparer.Ordinal);
    private static Task<ProbeProcessResult> RunAsync(string root)
    {
        ProcessStartInfo start = new("dotnet");
        foreach (string argument in new[] { ValidationHostBuild.WorkerPath(), "--scenario", "embedded-consumer", "--state-root", root })
        {
            start.ArgumentList.Add(argument);
        }
        return ProbeProcess.RunAsync(start);
    }
}
