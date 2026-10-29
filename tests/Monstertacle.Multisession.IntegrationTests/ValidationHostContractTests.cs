using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>The public validation CLI never reports success for invalid or unapproved work.</summary>
public sealed class ValidationHostContractTests
{
    /// <summary>Malformed invocations fail explicitly rather than falling through to process success.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task RejectsMalformedArguments(int variant)
    {
        string[] arguments = variant switch
        {
            0 => [],
            1 => ["--scenario"],
            2 => ["--scenario", "deterministic"],
            3 => ["--scenario", "deterministic", "--scenario", "routing"],
            _ => ["--unknown", "SECRET-ARGUMENT", "--scenario", "deterministic"],
        };
        ProbeProcessResult result = await RunAsync(arguments);
        Assert.Equal(2, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("FAIL", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("InvalidArguments", report.RootElement.GetProperty("Reason").GetString());
        Assert.DoesNotContain("SECRET-ARGUMENT", result.Output + result.Error, StringComparison.Ordinal);
    }

    /// <summary>Every documented scenario is recognized before rejecting a relative storage root.</summary>
    [Theory]
    [InlineData("deterministic")]
    [InlineData("routing")]
    [InlineData("failures")]
    [InlineData("cancellation")]
    [InlineData("shutdown")]
    [InlineData("capacity")]
    [InlineData("workspace")]
    [InlineData("extensions")]
    [InlineData("recovery")]
    [InlineData("native-context")]
    [InlineData("isolation")]
    [InlineData("embedded-consumer")]
    [InlineData("retention")]
    [InlineData("soak")]
    [InlineData("limits")]
    [InlineData("live-openai")]
    [InlineData("live-gemini")]
    public async Task RejectsRelativeRootForEveryKnownScenario(string scenario)
    {
        ProbeProcessResult result = await RunAsync(["--scenario", scenario, "--state-root", "relative-root"]);
        Assert.Equal(2, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("FAIL", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("InvalidStateRoot", report.RootElement.GetProperty("Reason").GetString());
        Assert.Equal(scenario, report.RootElement.GetProperty("Scenario").GetString());
    }

    /// <summary>No vendor request or storage mutation occurs without live cost/account/storage approval.</summary>
    [Theory]
    [InlineData("live-openai")]
    [InlineData("live-gemini")]
    public async Task RefusesUnapprovedLiveScenarioAndPreservesExistingState(string scenario)
    {
        await using PrivateStoreDirectory directory = new();
        await directory.PrepareFileAsync("foreign-state", Contracts.StateFileMode.CreateNew, CancellationToken.None);
        string file = Path.Combine(directory.StateDirectory, "foreign-state");
        await File.WriteAllTextAsync(file, "preserve foreign data");
        string[] original = Directory.GetFileSystemEntries(directory.StateDirectory);
        ProbeProcessResult result = await RunAsync(["--scenario", scenario, "--state-root", directory.StateDirectory]);
        Assert.Equal(3, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("NOT RUN", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("LiveApprovalRequired", report.RootElement.GetProperty("Reason").GetString());
        Assert.Equal(original, Directory.GetFileSystemEntries(directory.StateDirectory));
        Assert.Equal("preserve foreign data", await File.ReadAllTextAsync(file));
    }

    private static Task<ProbeProcessResult> RunAsync(string[] arguments)
    {
        ProcessStartInfo start = new("dotnet");
        start.ArgumentList.Add(ValidationHostBuild.WorkerPath());
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        return ProbeProcess.RunAsync(start);
    }

    /// <summary>An unknown scenario is rejected without echoing its untrusted name or touching storage.</summary>
    [Fact]
    public async Task RejectsUnknownScenarioWithoutEchoingRawArguments()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp", "validation-unused-" + Guid.NewGuid().ToString("N"));
        ProbeProcessResult result = await RunAsync(["--scenario", "SECRET-SCENARIO", "--state-root", root]);
        Assert.Equal(2, result.ExitCode);
        using JsonDocument report = JsonDocument.Parse(result.Output);
        Assert.Equal("FAIL", report.RootElement.GetProperty("Status").GetString());
        Assert.Equal("UnknownScenario", report.RootElement.GetProperty("Reason").GetString());
        Assert.DoesNotContain("SECRET-SCENARIO", result.Output + result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(root));
    }
}
