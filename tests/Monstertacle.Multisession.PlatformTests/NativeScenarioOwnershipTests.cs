using Monstertacle.Multisession.PlatformTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;
using Xunit.Abstractions;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Exercises the actual host assembly and StateRoot on the matching native target.</summary>
public sealed class NativeScenarioOwnershipTests(ITestOutputHelper output)
{
    /// <summary>The actual offline CLI scenarios satisfy their receipts and release native ownership.</summary>
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
    [InlineData("limits")]
    public async Task ExecutesOfflineScenarioAndReleasesNativeState(string scenario)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            "multisession-cli-scenario-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));
        try
        {
            // The report stays with a failure: a native run that exits non-zero shows why, not only its exit code.
            using StringWriter report = new();
            int exit = await ScenarioRunner.RunAsync(["--scenario", scenario, "--state-root", root], report);
            Assert.True(exit == 0, "scenario " + scenario + " exited " + exit + ": " + report);
            string run = Assert.Single(Directory.GetDirectories(root, "run-*"));
            await using NativeLease reopened = NativeLease.OpenExisting(run);
            Assert.True(reopened.IsHeld);
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); } }
    }

    /// <summary>Scenario cleanup releases native state ownership before returning success. The test then deletes its own root;
    /// after a failure the root is kept as evidence and its path is reported.</summary>
    [Fact]
    public async Task ReleasesNativeStateAfterConcreteScenario()
    {
        OwnedScenarioRoot owned = OwnedScenarioRoot.UnderScratch("multisession-native-scenario-");
        try
        {
            ValidationRunState state = await ValidationStateRoot.CreateRunAsync(owned.Path, CancellationToken.None);
            string directory = state.Directory;
            ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("native-ownership", state, current =>
            {
                Assert.IsType<NativeLease>(current.Lease);
                Assert.True(current.Lease.IsHeld);
                return Task.CompletedTask;
            });
            Assert.Equal("PASS", report.Status);
            Assert.Empty(report.CleanupFailures);
            using (FileStream exclusive = new(Path.Combine(directory, ".native-owner-release-probe"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Assert.False(state.Lease.IsHeld);
            }
            owned.Passed();
        }
        catch (Exception primary)
        {
            owned.Report(primary, output.WriteLine);
            throw;
        }
        finally { owned.Finish(); }
    }
}
