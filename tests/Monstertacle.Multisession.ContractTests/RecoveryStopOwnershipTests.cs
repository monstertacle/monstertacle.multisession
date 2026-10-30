using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Stop cancels locally tracked recovery I/O before releasing its store ownership.</summary>
public sealed class RecoveryStopOwnershipTests
{
    /// <summary>Neither an uncooperative inspect nor reopen can outlive local runtime cleanup.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StopCancelsRecoveryWithoutPublishingLateEvidence(bool resolve, bool typed)
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new()
        {
            Capabilities = new(true, true, true, false, false),
            Inspecting = async (_, _, _) =>
            {
                entered.TrySetResult();
                await finish.Task;
                return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late observation", true, null, null);
            },
            Reopening = async (_, binding, _) =>
            {
                entered.TrySetResult();
                await finish.Task;
                return binding with { ContinuationState = "late-head" };
            },
        };
        OperationRecoveryScenario scenario = await OperationRecoveryScenario.CreateAsync(provider);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = new(scenario.Store, lease, [new("registered-adapter", "test-adapter", 1, provider)], []);
        await runtime.StartAsync(Options(), CancellationToken.None);
        Task operation = typed
            ? resolve ? runtime.Commands.DispatchAsync(new ResolveRecoveryCommand(scenario.Decision()))
                : runtime.Queries.DispatchAsync(new InspectRecoveryQuery(scenario.Case.Target))
            : resolve ? runtime.Recovery.ResolveRecoveryAsync(scenario.Decision(), CancellationToken.None).AsTask()
                : runtime.Recovery.InspectRecoveryAsync(scenario.Case.Target, CancellationToken.None).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            Assert.True(operation.IsCompleted);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.True(lease.IsDisposed);
            Assert.Equal(scenario.Case, scenario.Store.RecoveryState.Read(scenario.Case.Target));
            Assert.Single(scenario.Store.Reservations);
            Assert.Equal(ExecutionCertainty.Unknown, Assert.Single(scenario.Store.Data.Requests.Values).Certainty);
        }
        finally
        {
            finish.TrySetResult();
            await Record.ExceptionAsync(() => operation);
        }
    }
}
