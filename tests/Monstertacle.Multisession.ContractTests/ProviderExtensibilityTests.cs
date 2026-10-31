using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Consumes two native wire contracts and a third host-owned agent adapter through one public runtime.</summary>
public sealed class ProviderExtensibilityTests
{
    /// <summary>Nine inputs retain session/model/root/history identity while three genuinely different sends overlap.</summary>
    [Fact]
    public async Task RunsNativeProvidersAndConsumerAdapterWithoutCoreSpecialCases()
    {
        await using NativeProviderHost host = new();
        IReadOnlyList<ProviderRun> runs = await host.StartAndScheduleAsync();
        await host.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, host.Store.Reservations.Count);
        foreach (ProviderRun run in runs)
        {
            Assert.Equal(RequestState.Running, (await host.Runtime.Requests.GetRequestAsync(run.Receipts[0].RequestId, CancellationToken.None))?.State);
        }
        host.Release();
        await host.Completed.WaitAsync(TimeSpan.FromSeconds(5));
        foreach (ProviderRun run in runs)
        {
            Page<HistoryEntry> history = await host.Runtime.Sessions.ReadHistoryAsync(run.Session.SessionId, 100, null, CancellationToken.None);
            Assert.Equal(run.Inputs, history.Items.Select(item => item.Input));
            Assert.Equal(run.Inputs, history.Items.Select(item => item.Result.Output));
            Assert.Equal(run.Receipts.Select(item => item.RequestId), history.Items.Select(item => item.RequestId));
            Assert.Equal(run.Receipts.Select(item => item.Sequence), history.Items.Select(item => item.Sequence));
            Assert.All(history.Items, item => Assert.Equal(RequestState.Succeeded, item.Result.Outcome));
            SessionSnapshot? current = await host.Runtime.Sessions.GetSessionAsync(run.Session.SessionId, CancellationToken.None);
            Assert.Equal(run.Session.Context.ModelId, current?.Context.ModelId);
            Assert.Equal(3, current?.BindingRevision);
        }
        Assert.Equal(9, runs.SelectMany(run => run.Receipts).Select(item => item.RequestId).Distinct().Count());
        Assert.Equal(runs[0].Inputs, host.Conversations.Inputs);
        Assert.Equal(runs[1].Inputs, host.Interactions.Inputs);
        Assert.Equal(runs[2].Inputs, host.External.Calls.Select(call => call.Input));
        Assert.All(host.External.Calls, call => Assert.Equal(ExecutionKind.AgentTask, call.Session.ExecutionKind));
        Assert.Equal("conv_runtime", host.Store.FindSession(runs[0].Session.SessionId)?.Binding?.SessionReference);
        Assert.Equal("interaction_runtime_1", host.Store.FindSession(runs[1].Session.SessionId)?.Binding?.SessionReference);
        Assert.Equal("interaction_runtime_3", host.Store.FindSession(runs[1].Session.SessionId)?.Binding?.ContinuationState);
        Assert.Equal(5, host.Conversations.Paths.Count);
        Assert.Equal(4, host.Interactions.Paths.Count);
        Assert.Empty((await host.Runtime.Recovery.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items);
        await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}
