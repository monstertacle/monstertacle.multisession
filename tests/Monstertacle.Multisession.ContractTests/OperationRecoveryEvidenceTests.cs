using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Known lazy identity evidence is distinct from publishing a confirmed continuation.</summary>
public sealed class OperationRecoveryEvidenceTests
{
    /// <summary>Recovery uses the persisted first native identity and publishes its head only after explicit confirmation.</summary>
    [Fact]
    public async Task ResolvesFirstLazyExecutionOnlyFromPersistedIdentity()
    {
        OperationAwareProvider provider = new()
        {
            Recovering = (binding, operation, token) =>
            {
                Assert.Equal("observed-first", binding.SessionReference);
                Assert.Null(binding.ContinuationState);
                Assert.Equal("new-operation", operation);
                return ValueTask.FromResult(binding with { ContinuationState = "verified-first-head" });
            },
        };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider, lazy: true);
        Assert.Equal("observed-first", scenario.Case.Binding?.SessionReference);
        Assert.Null(scenario.Case.Binding?.ContinuationState);
        Assert.Null(scenario.Store.FindSession(scenario.Case.SessionId)?.Binding?.SessionReference);
        await scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None);
        Assert.Equal("observed-first", scenario.Store.FindSession(scenario.Case.SessionId)?.Binding?.SessionReference);
        Assert.Equal("verified-first-head", scenario.Store.FindSession(scenario.Case.SessionId)?.Binding?.ContinuationState);
        Assert.Equal(1, scenario.Store.FindSession(scenario.Case.SessionId)?.BindingRevision);
        Assert.Empty(scenario.Store.Reservations);
    }

    /// <summary>A concurrent close invalidates previously gathered native evidence before binding publication.</summary>
    [Fact]
    public async Task RechecksCaseRevisionAfterOperationAwareNativeIo()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        OperationAwareProvider provider = new()
        {
            Recovering = async (binding, operation, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                return binding with { ContinuationState = "late-head" };
            },
        };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider);
        Task resolving = scenario.Service.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None).AsTask();
        await Task.WhenAny(entered.Task, resolving).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(entered.Task.IsCompletedSuccessfully);
        await scenario.Service.ResolveRecoveryAsync(scenario.Decision(RecoveryDecisionKind.CloseSession), CancellationToken.None);
        release.TrySetResult();
        SessionOperationException error = await Assert.ThrowsAsync<SessionOperationException>(() => resolving);
        Assert.Equal(ErrorCode.RevisionConflict, error.Failure.Code);
        Assert.Equal(scenario.Case.Binding, scenario.Store.FindSession(scenario.Case.SessionId)?.Binding);
        Assert.Single(scenario.Store.Reservations);
    }
}
