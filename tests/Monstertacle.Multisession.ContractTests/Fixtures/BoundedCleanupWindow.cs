using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

/// <summary>
/// The observed stop window of <c>RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses</c> (T020, T022), shared by that
/// test and its controls. The original test uses the defaults; a control adds only owned preparation (a held provider or a held
/// acceptance) and its owned release. The functional assertions and bounds are the original ones.
/// </summary>
internal sealed class BoundedCleanupWindow
{
    private const int _cleanupEntryWaitObserved = 0;
    private const int _stopFailureObserved = 1;

    internal ControlledProvider Provider { get; init; } = new();
    internal bool WithProfile { get; init; }
    internal Func<SubmitRequest, CancellationToken, ValueTask>? CommittingAcceptance { get; init; }
    internal Func<SessionRuntime, Task>? Prepare { get; init; }
    internal Action? Release { get; init; }

    internal SessionRuntime? Runtime { get; private set; }
    internal bool CleanupRan { get; private set; }
    internal bool LeaseDisposed { get; private set; }

    internal async Task RunAsync(Action<string> write)
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            Disposing = async () => { entered.TrySetResult(); await finish.Task; },
            CommittingAcceptance = CommittingAcceptance,
            IgnoreAcceptanceCancellation = CommittingAcceptance is not null,
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        SessionRuntime runtime = Runtime = WithProfile
            ? new(store, lease, [new("registered-adapter", "controlled", 1, Provider)],
                [new("profile", "registered-adapter", "model", ExecutionKind.ModelResponse, new("revision", new Dictionary<string, string>()))])
            : Runtime(store, Provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        if (Prepare is not null) { await Prepare(runtime); }
        SchedulingSampler sampler = SchedulingSampler.Started(); // Diagnostic only, written after the window; see follow-up-windows-elapsed-bounds.md.
        StageObservation stages = new("T020", "cleanup-entry-wait-observed", "stop-failure-observed");
        long began = Stopwatch.GetTimestamp();
        try
        {
            await stages.ObserveAsync(began, async () =>
            {
                Task stopping = runtime.StopAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None).AsTask();
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                stages.Mark(_cleanupEntryWaitObserved);
                try
                {
                    SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
                    stages.Mark(_stopFailureObserved);
                    Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
                    Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(1));
                    Assert.True(lease.IsHeld);
                }
                finally
                {
                    CleanupRan = true;
                    Release?.Invoke();
                    finish.TrySetResult();
                    await Record.ExceptionAsync(() => stopping);
                    await runtime.DisposeAsync();
                }
            }, line => { Line = line; write(line); }, () => stages.Append(sampler.Freeze(began, stages.Stamp(_stopFailureObserved)).Format()));
        }
        finally { LeaseDisposed = lease.IsDisposed; }
        Assert.True(lease.IsDisposed);
    }

    internal string? Line { get; private set; }
}
