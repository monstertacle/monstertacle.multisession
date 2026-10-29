using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks owned store startup and controlled conservative restart before any worker send.</summary>
public sealed class RuntimeStartupTests
{
    private static async Task<RequestSnapshot> WaitForSuccessAsync(SessionRuntime runtime, Guid requestId)
    {
        using CancellationTokenSource limit = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            limit.Token.ThrowIfCancellationRequested();
            RequestSnapshot? request = await runtime.Requests.GetRequestAsync(requestId, limit.Token);
            if (request?.State == RequestState.Succeeded) { return request; }
            await Task.Yield();
        }
    }

    /// <summary>Queued state is discovered at start without an admission wakeup in this process.</summary>
    [Fact]
    public async Task ProcessesAlreadyQueuedRequestsWithoutHostSignal()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        RequestReceipt receipt = await store.CommitAcceptanceAsync(new(session.SessionId, "stored input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, call.Input, true, binding, null)),
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        Assert.Equal("stored input", (await WaitForSuccessAsync(runtime, receipt.RequestId)).Result?.Output);
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    /// <summary>An old dispatch remains held and is never converted into send permission after start.</summary>
    [Fact]
    public async Task RecoversRunningAsUnknownWhileIndependentQueueContinues()
    {
        InMemorySessionStore store = new();
        SessionSnapshot original = SchedulingData.Session();
        SessionSnapshot independent = SchedulingData.Session();
        await store.CreateSessionAsync(original, CancellationToken.None);
        await store.CreateSessionAsync(independent, CancellationToken.None);
        RequestReceipt old = await store.CommitAcceptanceAsync(new(original.SessionId, "old execution", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation? held = await store.TryReserveHeadAsync(2, CancellationToken.None);
        Assert.NotNull(held);
        await store.CommitAcceptanceAsync(new(original.SessionId, "blocked follower", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        RequestReceipt other = await store.CommitAcceptanceAsync(new(independent.SessionId, "independent", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ConcurrentQueue<Guid> sends = new();
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) =>
            {
                sends.Enqueue(call.RequestId);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, call.Input, true, binding, null));
            },
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        await WaitForSuccessAsync(runtime, other.RequestId);
        RequestSnapshot? recovered = await runtime.Requests.GetRequestAsync(old.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Interrupted, recovered?.State);
        Assert.Equal(ExecutionCertainty.Unknown, recovered?.Certainty);
        Assert.NotNull(await runtime.Recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, old.RequestId), CancellationToken.None));
        Assert.Equal(other.RequestId, Assert.Single(sends));
        Assert.Equal(held, store.Reservations[held.DispatchId]);
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    /// <summary>An unfinished opening gets its own case and is not automatically opened or reopened.</summary>
    [Fact]
    public async Task ExposesOpeningCaseWithoutCreatingNativeIdentity()
    {
        InMemorySessionStore store = new();
        SessionSnapshot opening = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening, Binding = null };
        await store.CreateSessionAsync(opening, CancellationToken.None);
        ControlledProvider provider = new()
        {
            Opening = context => throw new InvalidOperationException("An uncertain opening must never be retried."),
            Reopening = (context, binding, token) => throw new InvalidOperationException("No binding exists to reopen."),
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        RecoveryCaseSnapshot? item = await runtime.Recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, opening.SessionId), CancellationToken.None);
        Assert.NotNull(item);
        Assert.Null(item.RequestId);
        Assert.Equal(SessionBlockReason.RecoveryRequired, (await runtime.Sessions.GetSessionAsync(opening.SessionId, CancellationToken.None))?.BlockReason);
        Assert.False(provider.Started.Task.IsCompleted);
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

}
