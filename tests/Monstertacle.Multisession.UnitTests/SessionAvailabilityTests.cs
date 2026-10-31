using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Local registration repair cannot clear unrelated native or recovery restrictions.</summary>
public sealed class SessionAvailabilityTests
{
    /// <summary>Only blocks explicitly caused by configuration are eligible for automatic configuration repair.</summary>
    [Theory]
    [InlineData(SessionBlockReason.None, false, false, SessionBlockReason.ProviderUnavailable, true)]
    [InlineData(SessionBlockReason.None, false, true, SessionBlockReason.None, false)]
    [InlineData(SessionBlockReason.ProviderUnavailable, true, true, SessionBlockReason.None, false)]
    [InlineData(SessionBlockReason.ProviderUnavailable, true, false, SessionBlockReason.ProviderUnavailable, true)]
    [InlineData(SessionBlockReason.ProviderUnavailable, false, true, SessionBlockReason.ProviderUnavailable, false)]
    [InlineData(SessionBlockReason.ProviderUnavailable, false, false, SessionBlockReason.ProviderUnavailable, false)]
    [InlineData(SessionBlockReason.RecoveryRequired, true, true, SessionBlockReason.RecoveryRequired, false)]
    [InlineData(SessionBlockReason.RecoveryRequired, false, false, SessionBlockReason.RecoveryRequired, false)]
    public void PreservesBlockProvenanceAndRecoveryPriority(SessionBlockReason current, bool configurationBlocked, bool available, SessionBlockReason expected, bool expectedFlag)
    {
        SessionSnapshot session = SchedulingData.Session() with { BlockReason = current, ConfigurationUnavailable = configurationBlocked };
        SessionSnapshot result = SessionAvailabilityPolicy.ForConfiguration(session, available);
        Assert.Equal(expected, result.BlockReason);
        Assert.Equal(expectedFlag, result.ConfigurationUnavailable);
        Assert.Equal(session.Binding, result.Binding);
        Assert.Equal(session.BindingRevision, result.BindingRevision);
        Assert.Equal(session.Context, result.Context);
    }
}
