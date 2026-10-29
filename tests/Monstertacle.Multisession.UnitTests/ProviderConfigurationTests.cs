using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks explicit, ordinal host configuration before any native operation.</summary>
public sealed class ProviderConfigurationTests
{
    private static ProviderRegistration Registration(string id = "provider") => new(id, "controlled", 1, new ControlledProvider());
    private static AgentProfile Profile(string id = "profile", string provider = "provider")
        => new(id, provider, "opaque model/deployment", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()));

    /// <summary>Rejects malformed registrations at construction with sanitized public errors.</summary>
    [Theory]
    [InlineData("empty-id")]
    [InlineData("empty-adapter")]
    [InlineData("bad-version")]
    [InlineData("null-provider")]
    [InlineData("duplicate")]
    public void RejectsInvalidRegistrations(string kind)
    {
        ProviderRegistration registration = kind switch
        {
            "empty-id" => Registration(" "),
            "empty-adapter" => Registration() with { AdapterId = "" },
            "bad-version" => Registration() with { ContractVersion = 0 },
            "null-provider" => Registration() with { Provider = null! },
            _ => Registration(),
        };
        ProviderRegistration[] registrations = kind == "duplicate" ? [registration, registration] : [registration];
        SessionOperationException error = Assert.Throws<SessionOperationException>(() => new SessionCatalog(new InMemorySessionStore(), registrations, []));
        Assert.Equal(ErrorCode.InvalidConfiguration, error.Failure.Code);
        Assert.Null(error.InnerException);
    }

    /// <summary>Rejects missing references and invalid profiles without trying native Open.</summary>
    [Theory]
    [InlineData("empty-id")]
    [InlineData("missing-provider")]
    [InlineData("empty-model")]
    [InlineData("empty-revision")]
    [InlineData("invalid-kind")]
    [InlineData("null-settings")]
    [InlineData("null-values")]
    [InlineData("duplicate")]
    public void RejectsInvalidProfiles(string kind)
    {
        AgentProfile profile = kind switch
        {
            "empty-id" => Profile(""),
            "missing-provider" => Profile(provider: "private-unknown-provider"),
            "empty-model" => Profile() with { ModelId = " " },
            "empty-revision" => Profile() with { Settings = new("", new Dictionary<string, string>()) },
            "invalid-kind" => Profile() with { ExecutionKind = (ExecutionKind)99 },
            "null-settings" => Profile() with { Settings = null! },
            "null-values" => Profile() with { Settings = new("revision", null!) },
            _ => Profile(),
        };
        AgentProfile[] profiles = kind == "duplicate" ? [profile, profile] : [profile];
        SessionOperationException error = Assert.Throws<SessionOperationException>(() => new SessionCatalog(new InMemorySessionStore(), [Registration()], profiles));
        Assert.Equal(ErrorCode.InvalidConfiguration, error.Failure.Code);
        Assert.DoesNotContain("private-unknown-provider", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Freezes nested profile settings at configuration time, before session creation.</summary>
    [Fact]
    public async Task FreezesConfigurationBeforeHostMutatesProfileValues()
    {
        Dictionary<string, string> values = new() { ["instruction"] = "original" };
        AgentProfile profile = Profile() with { Settings = new("revision", values) };
        SessionCatalog catalog = new(new InMemorySessionStore(), [Registration()], [profile]);
        values["instruction"] = "changed";
        SessionSnapshot session = await catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), null), CancellationToken.None);
        Assert.Equal("original", session.Context.Settings["instruction"]);
    }

    /// <summary>Case distinguishes registrations/profiles and several profiles may use the same opaque model.</summary>
    [Fact]
    public async Task KeepsOrdinalIdsAndAllowsSharedModel()
    {
        SessionCatalog catalog = new(new InMemorySessionStore(), [Registration(), Registration("Provider")], [Profile(), Profile("Profile", "Provider")]);
        SessionSnapshot first = await catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), null), CancellationToken.None);
        SessionSnapshot second = await catalog.CreateSessionAsync("Profile", new(Path.GetTempPath(), null), CancellationToken.None);
        Assert.Equal("provider", first.ProviderId);
        Assert.Equal("Provider", second.ProviderId);
        Assert.Equal(first.Context.ModelId, second.Context.ModelId);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }
}
