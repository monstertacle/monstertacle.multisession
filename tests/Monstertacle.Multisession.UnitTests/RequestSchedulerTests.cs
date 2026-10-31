using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Scheduling;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks reservations against controlled atomic state, not SQLite durability or real provider execution.</summary>
public sealed class RequestSchedulerTests
{
    private static async ValueTask<RequestReceipt> SubmitAsync(InMemorySessionStore store, string? writeKey = null)
    {
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        return await admission.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "unchanged input", TimeSpan.FromSeconds(10), writeKey, writeKey is not null), CancellationToken.None);
    }

    /// <summary>The returned dispatch already owns its slot and exposes Running/InProgress.</summary>
    [Fact]
    public async Task ReservesBeforePublishingDispatchIdentity()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        RequestReceipt receipt = await SubmitAsync(store, "writer");
        RequestScheduler scheduler = new(store, RuntimeOptionsTests.ValidOptions());

        ExecutionReservation? reservation = await scheduler.TryReserveAsync(CancellationToken.None);

        Assert.NotNull(reservation);
        Assert.NotEqual(Guid.Empty, reservation.DispatchId);
        Assert.Equal(receipt.RequestId, reservation.RequestId);
        Assert.Equal("writer", reservation.WorkspaceKey);
        Assert.Equal(clock.GetUtcNow(), reservation.ReservedUtc);
        Assert.Equal(reservation, store.Reservations[reservation.DispatchId]);
        RequestSnapshot? request = await ((ISessionStore)store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.NotNull(request);
        Assert.Equal(RequestState.Running, request.State);
        Assert.Equal(ExecutionCertainty.InProgress, request.Certainty);
        Assert.Equal(reservation.SessionId, request.SessionId);
        Assert.Equal(receipt.Sequence, request.Sequence);
        Assert.Null(await scheduler.TryReserveAsync(CancellationToken.None));
    }

    /// <summary>The scheduler cannot expose an identity before its reservation transaction finishes.</summary>
    [Fact]
    public async Task WaitsForReservationTransaction()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            Reserving = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); },
        };
        await SubmitAsync(store);
        RequestScheduler scheduler = new(store, RuntimeOptionsTests.ValidOptions());

        Task<ExecutionReservation?> pending = scheduler.TryReserveAsync(CancellationToken.None).AsTask();
        Task first = await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(entered.Task, first);
        Assert.False(pending.IsCompleted);
        Assert.Empty(store.Reservations);
        release.TrySetResult();
        Assert.NotNull(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>Cancelled waiting does not claim a committed reservation or start external work.</summary>
    [Fact]
    public async Task PropagatesCancellationToPendingReservation()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            Reserving = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); },
        };
        await SubmitAsync(store);
        using CancellationTokenSource cancellation = new();
        RequestScheduler scheduler = new(store, RuntimeOptionsTests.ValidOptions());
        Task<ExecutionReservation?> pending = scheduler.TryReserveAsync(cancellation.Token).AsTask();
        Task first = await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(entered.Task, first);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(store.Reservations);
    }

    /// <summary>Independent scheduler objects share the store's capacity rather than each granting their own slots.</summary>
    [Fact]
    public async Task SharesSlotsAcrossConcurrentSchedulers()
    {
        InMemorySessionStore store = new();
        for (int i = 0; i < 8; i++)
        {
            await SubmitAsync(store);
        }

        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2 };
        RequestScheduler first = new(store, options);
        RequestScheduler second = new(store, options);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExecutionReservation?>[] contenders = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return await (index % 2 == 0 ? first : second).TryReserveAsync(CancellationToken.None);
        })).ToArray();

        start.TrySetResult();
        ExecutionReservation?[] results = await Task.WhenAll(contenders).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, results.Count(result => result is not null));
        Assert.Equal(2, store.Reservations.Count);
        Assert.Equal(2, store.Reservations.Values.Select(item => item.SessionId).Distinct().Count());
        Assert.Equal(2, store.Reservations.Values.Select(item => item.DispatchId).Distinct().Count());
    }

    /// <summary>An interrupted local result retains its slot, workspace and session while another workspace uses remaining capacity.</summary>
    [Fact]
    public async Task KeepsUnknownReservationWhileUsingIndependentCapacity()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        RequestReceipt original = await SubmitAsync(store, "shared");
        clock.Advance(TimeSpan.FromSeconds(1));
        RequestReceipt blocked = await SubmitAsync(store, "shared");
        clock.Advance(TimeSpan.FromSeconds(1));
        RequestReceipt independent = await SubmitAsync(store, "other");
        RequestScheduler scheduler = new(store, RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2 });
        ExecutionReservation? held = await scheduler.TryReserveAsync(CancellationToken.None);
        Assert.NotNull(held);
        Assert.Equal(original.RequestId, held.RequestId);

        await store.MarkUncertainAsync(held.DispatchId, new(ErrorCode.Interrupted, ExecutionCertainty.Unknown), CancellationToken.None);
        ExecutionReservation? allowed = await scheduler.TryReserveAsync(CancellationToken.None);

        Assert.NotNull(allowed);
        Assert.Equal(independent.RequestId, allowed.RequestId);
        Assert.Equal(held, store.Reservations[held.DispatchId]);
        Assert.Equal(2, store.Reservations.Count);
        Assert.Null(await scheduler.TryReserveAsync(CancellationToken.None));
        ISessionStore queries = store;
        Assert.Equal(RequestState.Queued, (await queries.GetRequestAsync(blocked.RequestId, CancellationToken.None))?.State);
        RequestSnapshot? uncertain = await queries.GetRequestAsync(original.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Interrupted, uncertain?.State);
        Assert.Equal(ExecutionCertainty.Unknown, uncertain?.Certainty);
    }

    /// <summary>Invalid runtime limits are refused before the scheduler can request a store operation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidConfiguration(int limit)
    {
        SessionOperationException failure = Assert.Throws<SessionOperationException>(() =>
            new RequestScheduler(new InMemorySessionStore(), RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = limit }));

        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
    }
}
