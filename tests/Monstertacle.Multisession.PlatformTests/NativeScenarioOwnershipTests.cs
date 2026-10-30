using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Exercises the actual host assembly and StateRoot on the matching native target.</summary>
public sealed class NativeScenarioOwnershipTests
{
    /// <summary>Scenario cleanup releases native state ownership before returning success.</summary>
    [Fact]
    public async Task ReleasesNativeStateAfterConcreteScenario()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            "multisession-native-scenario-" + Guid.NewGuid().ToString("N"));
        ValidationRunState state = await ValidationStateRoot.CreateRunAsync(root, CancellationToken.None);
        string directory = state.Directory;
        ValidationReport report = await ScenarioRunner.ExecuteOwnedAsync("native-ownership", state, current =>
        {
            Assert.IsType<NativeLease>(current.Lease);
            Assert.True(current.Lease.IsHeld);
            return Task.CompletedTask;
        });
        Assert.Equal("PASS", report.Status);
        Assert.Empty(report.CleanupFailures);
        using FileStream exclusive = new(Path.Combine(directory, ".native-owner-release-probe"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        Assert.False(state.Lease.IsHeld);
    }
}
