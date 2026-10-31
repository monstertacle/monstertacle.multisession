using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Exercises actual controlled Sends across sessions, providers and shared capacity.</summary>
public sealed class DispatchConcurrencyTests
{
    /// <summary>Two providers overlap under the same global limit; 100 inputs/results keep their ten session identities.</summary>
    [Fact]
    public async Task RoutesHundredRequestsAcrossTenSessionsWithOverlappingProviders()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider first = DispatchLoadHost.Provider(release.Task);
        ControlledProvider second = DispatchLoadHost.Provider(release.Task);
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2 };
        var host = DispatchLoadHost.Create(store, first, second, options);
        List<SessionSnapshot> sessions = [];
        for (int i = 0; i < 10; i++)
        {
            sessions.Add(await host.Catalog.CreateSessionAsync(Guid.NewGuid(), i % 2 == 0 ? "one" : "two", new(Path.GetTempPath(), null), CancellationToken.None));
        }
        List<(RequestReceipt Receipt, Guid SessionId, string Input)> accepted = [];
        for (int i = 0; i < 100; i++)
        {
            SessionSnapshot session = sessions[i % sessions.Count];
            string input = $"request-{i}";
            RequestReceipt receipt = await host.Admission.SubmitAsync(Guid.NewGuid(), new(session.SessionId, input, TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            accepted.Add((receipt, session.SessionId, input));
            clock.Advance(TimeSpan.FromTicks(1));
        }

        Task<bool> left = host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        Task<bool> right = host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await Task.WhenAll(first.Started.Task, second.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(left.IsCompleted);
        Assert.False(right.IsCompleted);
        Assert.Equal(2, store.Reservations.Count);
        Assert.False(await host.Dispatcher.TryDispatchAsync(CancellationToken.None));
        release.TrySetResult();
        Assert.All(await Task.WhenAll(left, right).WaitAsync(TimeSpan.FromSeconds(5)), Assert.True);
        int remaining = 0;
        while (await host.Dispatcher.TryDispatchAsync(CancellationToken.None)) { remaining++; }
        Assert.Equal(98, remaining);
        Assert.Equal(100, store.DispatchState.History.Count);
        Assert.Equal(100, accepted.Select(item => item.Receipt.RequestId).Distinct().Count());
        foreach (var item in accepted)
        {
            RequestSnapshot? result = await host.Admission.GetRequestAsync(item.Receipt.RequestId, CancellationToken.None);
            Assert.Equal(item.SessionId, result?.SessionId);
            Assert.Equal(item.Receipt.Sequence, result?.Sequence);
            Assert.Equal(item.Input, result?.Result?.Output);
            Assert.Equal(RequestState.Succeeded, result?.State);
        }
        Assert.All(sessions, session => Assert.Equal(10, store.FindSession(session.SessionId)?.BindingRevision));
    }

    /// <summary>Two running Sends plus eight simultaneous submissions yield three admissions and five QueueFull errors.</summary>
    [Fact]
    public async Task KeepsTwoSendsActiveWhileQueueAdmitsOnlyThreeOfEight()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider first = DispatchLoadHost.Provider(release.Task);
        ControlledProvider second = DispatchLoadHost.Provider(release.Task);
        InMemorySessionStore store = new();
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2, MaxQueuedRequests = 3 };
        var host = DispatchLoadHost.Create(store, first, second, options);
        List<SessionSnapshot> sessions = [];
        for (int i = 0; i < 10; i++)
        {
            sessions.Add(await host.Catalog.CreateSessionAsync(Guid.NewGuid(), i % 2 == 0 ? "one" : "two", new(Path.GetTempPath(), null), CancellationToken.None));
        }
        await host.Admission.SubmitAsync(Guid.NewGuid(), new(sessions[0].SessionId, "running-one", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        Task<bool> left = host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await first.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.Admission.SubmitAsync(Guid.NewGuid(), new(sessions[1].SessionId, "running-two", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        Task<bool> right = host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask();
        await second.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] submissions = sessions.Skip(2).Select(session => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                await host.Admission.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "new queued input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
                return true;
            }
            catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.QueueFull) { return false; }
        })).ToArray();
        start.TrySetResult();
        bool[] outcomes = await Task.WhenAll(submissions).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, outcomes.Count(accepted => accepted));
        Assert.Equal(5, outcomes.Count(accepted => !accepted));
        Assert.Equal(2, store.Reservations.Count);
        Assert.False(left.IsCompleted);
        Assert.False(right.IsCompleted);
        Assert.False(await host.Dispatcher.TryDispatchAsync(CancellationToken.None));
        release.TrySetResult();
        Assert.All(await Task.WhenAll(left, right), Assert.True);
        int completed = 2;
        while (await host.Dispatcher.TryDispatchAsync(CancellationToken.None)) { completed++; }
        Assert.Equal(5, completed);
        Assert.Equal(5, store.DispatchState.History.Count);
    }
}
