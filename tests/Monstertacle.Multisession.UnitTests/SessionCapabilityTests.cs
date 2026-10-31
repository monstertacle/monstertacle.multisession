using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks explicit adapter capabilities before any native execution or state creation.</summary>
public sealed class SessionCapabilityTests
{
    /// <summary>Never substitutes a model response for a requested agent task, or vice versa.</summary>
    [Theory]
    [InlineData(ExecutionKind.AgentTask, true, false)]
    [InlineData(ExecutionKind.ModelResponse, false, true)]
    [InlineData((ExecutionKind)99, true, true)]
    public async Task RejectsUnsupportedExecutionKind(ExecutionKind requested, bool supportsModel, bool supportsAgent)
    {
        int opens = 0;
        ControlledProvider provider = new()
        {
            Capabilities = new(supportsModel, supportsAgent, false, false, false),
            Opening = _ => opens++,
        };
        InMemorySessionStore store = new();
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        AgentProfile[] profiles = [new("profile", "connection", "opaque", requested, new("revision", new Dictionary<string, string>()))];
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(async () =>
        {
            SessionCatalog catalog = new(store, registrations, profiles);
            await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        });

        Assert.Equal(Enum.IsDefined(requested) ? ErrorCode.UnsupportedProviderAction : ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Equal(0, opens);
    }

    /// <summary>A workspace-dependent backend cannot start on absent, relative or network paths.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/workspace")]
    [InlineData(@"\\server\share\workspace")]
    [InlineData("//server/share/workspace")]
    public async Task RejectsInvalidRequiredWorkspace(string? path)
    {
        int opens = 0;
        ControlledProvider provider = new()
        {
            Capabilities = new(false, true, false, false, true),
            Opening = _ => opens++,
        };
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        AgentProfile[] profiles = [new("profile", "connection", "opaque", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()))];
        SessionCatalog catalog = new(new InMemorySessionStore(), registrations, profiles);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(path, "workspace"), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, opens);
    }

    /// <summary>Uses stable failures rather than a fallback adapter when a host identity cannot be resolved.</summary>
    [Theory]
    [InlineData("missing", "connection", ErrorCode.NotFound)]
    [InlineData("PROFILE", "connection", ErrorCode.NotFound)]
    [InlineData("profile", "unregistered", ErrorCode.InvalidConfiguration)]
    public async Task RejectsUnresolvedHostSelection(string requestedProfile, string configuredConnection, ErrorCode expected)
    {
        int opens = 0;
        ControlledProvider provider = new() { Opening = _ => opens++ };
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        AgentProfile[] profiles = [new("profile", configuredConnection, "opaque", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()))];
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(async () =>
        {
            SessionCatalog catalog = new(new InMemorySessionStore(), registrations, profiles);
            await catalog.CreateSessionAsync(Guid.NewGuid(), requestedProfile, new(null, null), CancellationToken.None);
        });

        Assert.Equal(expected, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Equal(0, opens);
    }

    /// <summary>An optional workspace, when supplied, must still be an absolute local path.</summary>
    [Theory]
    [InlineData("relative/workspace")]
    [InlineData("//server/share/workspace")]
    public async Task RejectsMalformedOptionalWorkspace(string path)
    {
        int opens = 0;
        ControlledProvider provider = new() { Opening = _ => opens++ };
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        AgentProfile[] profiles = [new("profile", "connection", "opaque", ExecutionKind.ModelResponse, new("revision", new Dictionary<string, string>()))];
        SessionCatalog catalog = new(new InMemorySessionStore(), registrations, profiles);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(path, null), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, opens);
    }
}
