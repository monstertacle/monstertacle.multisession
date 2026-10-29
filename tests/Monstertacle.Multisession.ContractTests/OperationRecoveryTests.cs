using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Prevents recovery from silently continuing a chain from its obsolete confirmed head.</summary>
public sealed class OperationRecoveryTests
{
    /// <summary>The uncertain operation participates in the native check before the new head is published by CAS.</summary>
    [Fact]
    public async Task VerifiesUncertainOperationInsteadOfOnlyOldBinding()
    {
        OperationAwareProvider provider = new()
        {
            Recovering = (binding, operation, token) =>
            {
                Assert.Equal("new-operation", operation);
                return ValueTask.FromResult(binding with { ContinuationState = "verified-new-head" });
            },
        };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider);
        await scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None);
        Assert.Equal(1, provider.RecoveryCalls);
        Assert.Equal(0, provider.LegacyReopenCalls);
        Assert.Equal("verified-new-head", scenario.Store.FindSession(scenario.Case.SessionId)?.Binding?.ContinuationState);
        Assert.Empty(scenario.Store.Reservations);
        RequestSnapshot? request = await ((ISessionStore)scenario.Store).GetRequestAsync(scenario.Case.RequestId!.Value, CancellationToken.None);
        Assert.Equal(RequestState.Interrupted, request?.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request?.Certainty);
    }

    /// <summary>Advertising the requirement without implementing the port cannot fall back to an unsafe old-head reopen.</summary>
    [Fact]
    public async Task BlocksWhenRequiredPortIsMissing()
    {
        ControlledProvider provider = new() { Capabilities = new(true, true, true, false, false) { RequiresOperationAwareRecovery = true } };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedProviderAction, error.Failure.Code);
        Assert.Single(scenario.Store.Reservations);
        Assert.Equal(scenario.Case, await scenario.Service.GetRecoveryCaseAsync(scenario.Case.Target, CancellationToken.None));
    }

    /// <summary>A provider that cannot verify an absent operation reference keeps the case blocked.</summary>
    [Fact]
    public async Task BlocksWhenRequiredNativeEvidenceIsAbsent()
    {
        OperationAwareProvider provider = new();
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider, recordOperation: false);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedProviderAction, error.Failure.Code);
        Assert.Single(scenario.Store.Reservations);
        Assert.Equal(0, provider.LegacyReopenCalls);
    }

    /// <summary>Failed or incompatible native evidence cannot release the reservation or change the confirmed binding.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsFailedOrForeignNativeRecovery(bool throws)
    {
        OperationAwareProvider provider = new()
        {
            Recovering = (binding, operation, token) => throws ? throw new IOException("private native details")
                : ValueTask.FromResult(binding with { SessionReference = "foreign-identity" }),
        };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider);
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ProviderUnavailable, error.Failure.Code);
        Assert.DoesNotContain("private", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(scenario.Case.Binding, scenario.Store.FindSession(scenario.Case.SessionId)?.Binding);
        Assert.Single(scenario.Store.Reservations);
    }
}
