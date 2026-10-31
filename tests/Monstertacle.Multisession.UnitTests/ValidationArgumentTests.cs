using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks the host CLI's closed argument errors and live-approval boundary.</summary>
public sealed class ValidationArgumentTests
{
    /// <summary>Malformed flags and unowned path forms yield finite failure reports without executable options.</summary>
    [Fact]
    public void RejectsMalformedArgumentsAndUnsafeRootForms()
    {
        string root = Path.GetTempPath();
        (string[] Arguments, string Reason)[] cases =
        [
            ([], "InvalidArguments"),
            (["--scenario"], "InvalidArguments"),
            (["--scenario", "deterministic", "--scenario", "routing"], "InvalidArguments"),
            (["--state-root", root, "--state-root", root], "InvalidArguments"),
            (["--other", "value", "--state-root", root], "InvalidArguments"),
            (["--scenario", "unknown", "--state-root", root], "UnknownScenario"),
            (["--scenario", "deterministic", "--state-root", "relative"], "InvalidStateRoot"),
            (["--scenario", "deterministic", "--state-root", " "], "InvalidStateRoot"),
            (["--scenario", "deterministic", "--state-root", "bad\0root"], "InvalidStateRoot"),
            (["--scenario", "deterministic", "--state-root", "//host/share"], "InvalidStateRoot"),
            (["--scenario", "deterministic", "--state-root", @"\\host\share"], "InvalidStateRoot"),
        ];
        foreach (var item in cases)
        {
            ValidationReport? report = ValidationArguments.Parse(item.Arguments, out ValidationArguments? options);
            Assert.Null(options);
            Assert.Equal("FAIL", report?.Status);
            Assert.Equal(item.Reason, report?.Reason);
            Assert.Equal(2, report?.ExitCode);
        }
    }

    /// <summary>Flag order is independent and preserves the caller's explicit absolute root.</summary>
    [Fact]
    public void AcceptsReversedFlagOrder()
    {
        string root = Path.GetTempPath();
        Assert.Null(ValidationArguments.Parse(["--state-root", root, "--scenario", "routing"], out ValidationArguments? options));
        Assert.Equal(new ValidationArguments("routing", root), options);
    }

    /// <summary>Unapproved live scenarios create no local state and perform no provider operation.</summary>
    [Theory]
    [InlineData("live-openai")]
    [InlineData("live-gemini")]
    public async Task RejectsLiveExecutionBeforeCreatingState(string scenario)
    {
        string root = Path.Combine(Path.GetTempPath(), "multisession-no-live-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));
        Assert.Equal(3, await ScenarioRunner.RunAsync(["--scenario", scenario, "--state-root", root]));
        Assert.False(Directory.Exists(root));
        Assert.False(File.Exists(root));
    }
}
