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
/// <remarks>
/// T022 adds three stamps and one field, all read at freeze: when the store's cleanup was first entered, when the stop task ended,
/// when the test's own cleanup began, and the stop's outcome. SDK-T020-ENTRY-TIMEOUT-FIXTURE-CLEANUP-001: the entry wait is inside the
/// guarded body, so the window's cleanup (release, finish, dispose) runs when it fails; a cleanup failure is reported after the
/// measured failure, never instead of it.
/// </remarks>
internal sealed class BoundedCleanupWindow
{
    private const int _cleanupEntryWaitObserved = 0;
    private const int _stopFailureObserved = 1;
    private const int _cleanupEntered = 2;
    private const int _stopCompleted = 3;
    private const int _testCleanupBegan = 4;

    internal ControlledProvider Provider { get; init; } = new();
    internal bool WithProfile { get; init; }
    internal Func<SubmitRequest, CancellationToken, ValueTask>? CommittingAcceptance { get; init; }
    internal Func<SessionRuntime, Task>? Prepare { get; init; }
    internal Action? Release { get; init; }

    internal SessionRuntime? Runtime { get; private set; }
    internal bool CleanupRan { get; private set; }
    internal bool LeaseDisposed { get; private set; }
    internal string? Line { get; private set; }

    internal async Task RunAsync(Action<string> write)
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        long[] at = new long[2]; // [0] first cleanup entry, [1] stop task end; set once each, read at freeze.
        InMemorySessionStore store = new()
        {
            Disposing = async () =>
            {
                Interlocked.CompareExchange(ref at[0], Stopwatch.GetTimestamp(), 0);
                entered.TrySetResult();
                await finish.Task;
            },
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
        StageObservation stages = new("T020", "cleanup-entry-wait-observed", "stop-failure-observed", "cleanup-entered", "stop-completed", "test-cleanup-began");
        Task stopping = Task.CompletedTask;
        long began = Stopwatch.GetTimestamp();
        try
        {
            await stages.ObserveAsync(began, async () =>
            {
                stopping = runtime.StopAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None).AsTask();
                _ = stopping.ContinueWith(_ => Interlocked.CompareExchange(ref at[1], Stopwatch.GetTimestamp(), 0),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    stages.Mark(_cleanupEntryWaitObserved);
                    SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
                    stages.Mark(_stopFailureObserved);
                    Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
                    Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(1));
                    Assert.True(lease.IsHeld);
                }
                catch (Exception primary)
                {
                    // The measured failure stays first; a cleanup failure is reported with it, never instead of it.
                    stages.Mark(_testCleanupBegan);
                    List<Exception> unexpected = await CleanUpAsync(runtime, finish, stopping);
                    if (unexpected.Count > 0) { throw new AggregateException("Cleaning up the T020 window failed after the window failed.", [primary, .. unexpected]); }
                    throw;
                }
                stages.Mark(_testCleanupBegan);
                List<Exception> failures = await CleanUpAsync(runtime, finish, stopping);
                if (failures.Count > 0) { throw new AggregateException("Cleaning up the T020 window failed.", failures); }
            }, line => { Line = line; write(line); }, () =>
            {
                stages.Record(_cleanupEntered, Interlocked.Read(ref at[0]));
                stages.Record(_stopCompleted, Interlocked.Read(ref at[1]));
                stages.Append("stop-outcome=" + Outcome(stopping) + " " + sampler.Freeze(began, stages.Stamp(_stopFailureObserved)).Format());
            });
        }
        finally { LeaseDisposed = lease.IsDisposed; }
        Assert.True(lease.IsDisposed);
    }

    // The original cleanup order: release the owned preparation, let the store finish closing, observe the stop, dispose the
    // runtime. Every step runs; the failures are returned in order.
    private async Task<List<Exception>> CleanUpAsync(SessionRuntime runtime, TaskCompletionSource finish, Task stopping)
    {
        CleanupRan = true;
        List<Exception> failures = [];
        try { Release?.Invoke(); }
        catch (Exception failure) { failures.Add(failure); }
        finish.TrySetResult();
        await Record.ExceptionAsync(() => stopping);
        if (await Record.ExceptionAsync(async () => await runtime.DisposeAsync()) is { } disposal) { failures.Add(disposal); }
        return failures;
    }

    // The stop task's state at freeze: pending, completed, canceled, or faulted with the exception type and failure code.
    private static string Outcome(Task stopping) => stopping.Status switch
    {
        TaskStatus.RanToCompletion => "completed",
        TaskStatus.Canceled => "canceled",
        TaskStatus.Faulted => "faulted:" + stopping.Exception!.InnerException!.GetType().Name
            + (stopping.Exception.InnerException is SessionOperationException typed ? ":" + typed.Failure.Code : string.Empty),
        _ => "pending",
    };
}
