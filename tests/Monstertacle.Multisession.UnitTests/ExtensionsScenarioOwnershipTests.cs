using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>The extensions scenario owner keeps its workload failure when host disposal also fails (T016,
/// PR40-WIN-EXTENSIONS-STORAGEUNAVAILABLE-20261104): a disposal failure is added as cleanup and never replaces the primary.</summary>
public sealed class ExtensionsScenarioOwnershipTests
{
    /// <summary>A workload failure plus a failing host disposal reports the workload failure and the disposal as cleanup.</summary>
    [Fact]
    public async Task KeepsWorkloadFailureWhenHostDisposalAlsoFails()
    {
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        int storeDisposals = 0;
        InMemorySessionStore store = new()
        {
            Disposing = () =>
            {
                storeDisposals++;
                return ValueTask.FromException(new SessionOperationException(new(ErrorCode.StorageUnavailable)));
            },
        };
        ValidationReport report = await RunWithoutProvidersAsync(store, options);

        Assert.Equal("FAIL", report.Status);
        Assert.Equal(ErrorCode.NotFound, report.Failure?.Error.Code);
        Assert.Equal(new[] { ErrorCode.StorageUnavailable }, report.CleanupFailures.Select(item => item.Error.Code));
        Assert.Equal(1, storeDisposals);
    }

    /// <summary>Control: with a successful disposal the workload failure is reported alone, with no invented cleanup failure.</summary>
    [Fact]
    public async Task KeepsWorkloadFailureAloneWhenHostDisposalSucceeds()
    {
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        ValidationReport report = await RunWithoutProvidersAsync(new InMemorySessionStore(), options);

        Assert.Equal("FAIL", report.Status);
        Assert.Equal(ErrorCode.NotFound, report.Failure?.Error.Code);
        Assert.Empty(report.CleanupFailures);
    }

    // A runtime without the scenario's providers: the workload's first session creation fails with a typed NotFound.
    private static async Task<ValidationReport> RunWithoutProvidersAsync(InMemorySessionStore store, RuntimeOptions options)
    {
        SessionRuntime runtime = new(store, new TestStateDirectoryLease(options.StateDirectory), [], []);
        await runtime.StartAsync(options, CancellationToken.None);
        ProbeHost host = new(store, runtime, options);
        ProbeBarrier barrier = new(1);
        return await ScenarioRunner.ExecuteOwnedAsync("extensions", new(Guid.NewGuid(), new FailingProbeLease(options.StateDirectory)),
            _ => ExtensionsProbeFlow.ExecuteOwnedAsync(host, new(), barrier, new ThirdContractProvider(barrier.Release.Task), options.StateDirectory));
    }
}
