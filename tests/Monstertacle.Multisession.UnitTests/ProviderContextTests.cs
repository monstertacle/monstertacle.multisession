using Monstertacle.Multisession.Contracts;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks that a host cannot mutate the settings of an established session.</summary>
public sealed class ProviderContextTests
{
    /// <summary>Preserves the original profile even when the host reuses its mutable configuration.</summary>
    [Fact]
    public void FreezesSettingsAtSessionCreation()
    {
        Dictionary<string, string> settings = new(StringComparer.Ordinal) { ["instruction"] = "original" };
        SessionExecutionContext workspace = new(Path.GetTempPath(), "workspace-key");
        ProviderSessionContext context = new(Guid.NewGuid(), "provider/opaque-model", ExecutionKind.AgentTask, workspace, settings);

        settings["instruction"] = "changed";
        settings["new-setting"] = "later";

        Assert.Equal("original", context.Settings["instruction"]);
        Assert.False(context.Settings.ContainsKey("new-setting"));
        Assert.Equal("provider/opaque-model", context.ModelId);
        Assert.Equal(ExecutionKind.AgentTask, context.ExecutionKind);
        Assert.Same(workspace, context.ExecutionContext);
    }

    /// <summary>Preserves distinct technical setting names with ordinal case-sensitive semantics.</summary>
    [Fact]
    public void RetainsOrdinalSettingKeys()
    {
        Dictionary<string, string> settings = new(StringComparer.Ordinal) { ["Setting"] = "upper", ["setting"] = "lower" };
        ProviderSessionContext context = new(Guid.NewGuid(), "opaque", ExecutionKind.ModelResponse, new(null, null), settings);

        Assert.Equal("upper", context.Settings["Setting"]);
        Assert.Equal("lower", context.Settings["setting"]);
        Assert.False(context.Settings.ContainsKey("SETTING"));
    }
}
