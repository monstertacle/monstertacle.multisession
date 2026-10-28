using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Scheduling;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Controlled reservation/queue regressions; no provider Sends or production persistence are claimed.</summary>
public sealed class CapacityTests
{
    /// <summary>Eight concurrent submissions share three queued slots while two reservations remain occupied.</summary>
    [Fact]
    public async Task CountsOnlyQueuedRequestsAgainstAdmissionCapacity()
    {
        InMemorySessionStore store = new();
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2, MaxQueuedRequests = 3 };
        RequestAdmission admission = new(store, options);
        RequestScheduler scheduler = new(store, options);
        SessionSnapshot[] sessions = Enumerable.Range(0, 10).Select(_ => SchedulingData.Session()).ToArray();
        foreach (SessionSnapshot session in sessions)
        {
            await store.CreateSessionAsync(session, CancellationToken.None);
        }

        foreach (SessionSnapshot session in sessions.Take(2))
        {
            await admission.SubmitAsync(new(session.SessionId, "reserved input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            Assert.NotNull(await scheduler.TryReserveAsync(CancellationToken.None));
        }

        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] contenders = sessions.Skip(2).Select(session => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                await admission.SubmitAsync(new(session.SessionId, "queued input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
                return true;
            }
            catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.QueueFull)
            {
                return false;
            }
        })).ToArray();

        start.TrySetResult();
        bool[] accepted = await Task.WhenAll(contenders).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, accepted.Count(value => value));
        Assert.Equal(5, accepted.Count(value => !value));
        Assert.Equal(5, store.Submissions.Count);
        Assert.Equal(2, store.Reservations.Count);
        Assert.Null(await scheduler.TryReserveAsync(CancellationToken.None));
        RequestSnapshot?[] requests = await Task.WhenAll(store.Submissions.Keys
            .Select(id => admission.GetRequestAsync(id, CancellationToken.None).AsTask()));
        Assert.Equal(2, requests.Count(request => request?.State == RequestState.Running));
        Assert.Equal(3, requests.Count(request => request?.State == RequestState.Queued));
    }

    /// <summary>Concurrent writers cannot each claim the same workspace even with spare global slots.</summary>
    [Fact]
    public async Task SerializesSharedWorkspaceAcrossConcurrentReservations()
    {
        InMemorySessionStore store = new();
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 4 };
        RequestAdmission admission = new(store, options);
        RequestScheduler scheduler = new(store, options);
        for (int i = 0; i < 8; i++)
        {
            SessionSnapshot session = SchedulingData.Session();
            await store.CreateSessionAsync(session, CancellationToken.None);
            await admission.SubmitAsync(new(session.SessionId, "write input", TimeSpan.FromSeconds(10), "shared", true), CancellationToken.None);
        }

        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExecutionReservation?>[] contenders = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await scheduler.TryReserveAsync(CancellationToken.None);
        })).ToArray();

        start.TrySetResult();
        ExecutionReservation?[] results = await Task.WhenAll(contenders).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(results, result => result is not null);
        ExecutionReservation reservation = Assert.Single(store.Reservations.Values);
        Assert.Equal("shared", reservation.WorkspaceKey);
    }

    /// <summary>A second accepted request in one session remains queued while an independent session reserves a slot.</summary>
    [Fact]
    public async Task ReservesOnlyOneRequestPerSession()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        SessionSnapshot first = SchedulingData.Session();
        SessionSnapshot second = SchedulingData.Session();
        await store.CreateSessionAsync(first, CancellationToken.None);
        await store.CreateSessionAsync(second, CancellationToken.None);
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2 };
        RequestAdmission admission = new(store, options);
        RequestReceipt head = await admission.SubmitAsync(new(first.SessionId, "first", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestReceipt later = await admission.SubmitAsync(new(first.SessionId, "second", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));
        RequestReceipt other = await admission.SubmitAsync(new(second.SessionId, "independent", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestScheduler scheduler = new(store, options);

        Assert.Equal(head.RequestId, (await scheduler.TryReserveAsync(CancellationToken.None))?.RequestId);
        Assert.Equal(other.RequestId, (await scheduler.TryReserveAsync(CancellationToken.None))?.RequestId);

        Assert.Equal(RequestState.Queued, (await admission.GetRequestAsync(later.RequestId, CancellationToken.None))?.State);
        Assert.Equal(2, store.Reservations.Count);
    }
}
