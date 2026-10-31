using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Disposal owns a pending startup and cannot allow a runtime to become usable afterwards.</summary>
public sealed class StartupDisposeTests
{
    /// <summary>Concurrent disposal waits for actual preparation, fences workers and cleans transferred ownership once.</summary>
    [Fact]
    public async Task WaitsForPendingStartupAndNeverStartsWorkersAfterDisposal()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int cleanups = 0;
        InMemorySessionStore store = new()
        {
            ValidatingOptions = async (_, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); },
            Disposing = () => { Interlocked.Increment(ref cleanups); return ValueTask.CompletedTask; },
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        SessionRuntime runtime = Runtime(store, new(), lease);
        using CancellationTokenSource cancelStartup = new();
        Task starting = runtime.StartAsync(Options(), cancelStartup.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task first = runtime.DisposeAsync().AsTask();
        Task second = runtime.DisposeAsync().AsTask();
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.True(lease.IsHeld);
            Assert.Equal(0, cleanups);
            release.TrySetResult();
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => starting.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(runtime.Completion.IsCompletedSuccessfully);
            Assert.True(lease.IsDisposed);
            Assert.Equal(1, cleanups);
        }
        finally
        {
            cancelStartup.Cancel();
            release.TrySetResult();
            await Record.ExceptionAsync(() => starting.WaitAsync(TimeSpan.FromSeconds(5)));
            await Record.ExceptionAsync(() => Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)));
            await Record.ExceptionAsync(() => runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }
}
