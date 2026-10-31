using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Owned runtime sampling is independent of metric callbacks and business operations.</summary>
public sealed class RuntimeStatisticsMonitorTests
{
    /// <summary>Sampling publishes a cached snapshot, contains read failures, and withdraws data at stop.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SamplesWhileRunningAndWithdrawsAtStop(bool fail)
    {
        TaskCompletionSource sampled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            ReadingStatistics = token =>
            {
                sampled.TrySetResult();
                return fail ? ValueTask.FromException<RuntimeStatistics>(new IOException("SECRET-STORE")) : ValueTask.FromResult(new RuntimeStatistics(5, 2, 1));
            }
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(store: store);
        using SessionDiagnostics diagnostics = new();
        using MetricGaugeCapture metrics = new(diagnostics.InstanceId);
        await using SessionRuntime runtime = new(store, new TestStateDirectoryLease(scenario.Options.StateDirectory),
            [new("connection", "controlled", 1, scenario.Provider)], [], diagnostics: diagnostics);
        await runtime.StartAsync(scenario.Options, CancellationToken.None);
        await sampled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (fail ? diagnostics.FailureCount == 0 : metrics.Read().Count == 0) { await Task.Delay(10, deadline.Token); }
        if (fail) { Assert.Empty(metrics.Read()); }
        else { Assert.Equal(5, metrics.Read()["multisession.requests.queued"]); }
        RequestReceipt receipt = await runtime.Requests.SubmitAsync(new(scenario.Session.SessionId, "SECRET-INPUT", TimeSpan.FromSeconds(5), null, false), deadline.Token);
        Assert.NotEqual(Guid.Empty, receipt.RequestId);
        await runtime.StopAsync(TimeSpan.FromSeconds(5), deadline.Token);
        Assert.Empty(metrics.Read());
    }

    /// <summary>Shutdown does not await a diagnostic reader that ignores its cancellation token.</summary>
    [Fact]
    public async Task IgnoredReadCancellationCannotHoldRuntimeOwnership()
    {
        TaskCompletionSource sampled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<RuntimeStatistics> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new() { ReadingStatistics = token => { sampled.TrySetResult(); return new(pending.Task); } };
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        TestStateDirectoryLease lease = new(options.StateDirectory);
        using SessionDiagnostics diagnostics = new();
        await using SessionRuntime runtime = new(store, lease, [], [], diagnostics: diagnostics);
        await runtime.StartAsync(options, CancellationToken.None);
        try
        {
            await sampled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await runtime.StopAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
            Assert.True(lease.IsDisposed);
        }
        finally { pending.TrySetResult(new(0, 0, 0)); }
    }
}
