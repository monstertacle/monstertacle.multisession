using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Startup revalidation is bounded and cannot replace confirmed native continuation with a late reply.</summary>
public sealed class StartupReopenDeadlineTests
{
    /// <summary>A token-ignoring reopen times out locally and its late different identity is discarded.</summary>
    [Fact]
    public async Task BoundsUncooperativeReadWithoutAcceptingLateBinding()
    {
        using DispatchClock clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderBinding> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new()
        {
            Reopening = (_, _, _) => { entered.TrySetResult(); return new(finish.Task); },
        };
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = new(store, lease, [new("registered-adapter", "test-adapter", 1, provider)], [], clock);
        Task starting = runtime.StartAsync(Options(), CancellationToken.None).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Options().RequestTimeout, clock.DueTime);
            clock.Fire();
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            SessionSnapshot blocked = (await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))!;
            Assert.Equal(ErrorCode.TimedOut, blocked.AvailabilityError);
            Assert.Equal(SessionBlockReason.ProviderUnavailable, blocked.BlockReason);
            finish.TrySetResult(session.Binding! with { SessionReference = "foreign-late-binding" });
            Assert.Equal(session.Binding, (await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.Binding);
            await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        finally { finish.TrySetResult(session.Binding!); }
    }

    /// <summary>Neither recovery nor a native continuation blocker is automatically revalidated or cleared.</summary>
    [Theory]
    [InlineData(SessionBlockReason.RecoveryRequired)]
    [InlineData(SessionBlockReason.StorageFailure)]
    [InlineData(SessionBlockReason.ProviderUnavailable)]
    public async Task DoesNotReopenUnrelatedBlock(SessionBlockReason reason)
    {
        int reopens = 0;
        ControlledProvider provider = new()
        {
            Reopening = (_, binding, _) => { Interlocked.Increment(ref reopens); return ValueTask.FromResult(binding); },
        };
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session() with { BlockReason = reason };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        Assert.Equal(0, reopens);
        Assert.Equal(reason, (await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.BlockReason);
        Assert.Equal(session.Binding, (await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.Binding);
        await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }
}
