using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Consumes only the public runtime using controlled provider/store/lease contracts.</summary>
public sealed class ParallelSessionsTests
{
    private static ControlledProvider Provider(Task release, ConcurrentQueue<ProviderCall> calls, TaskCompletionSource entered) => new()
    {
        BindingForContext = context => new("controlled", 1, $"opaque-{context.SessionId:D}", null),
        Sending = async (call, binding, observe, token) =>
        {
            Assert.Equal($"opaque-{call.Session.SessionId:D}", binding.SessionReference);
            calls.Enqueue(call);
            entered.TrySetResult();
            await release.WaitAsync(token);
            return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, call.Input, true, binding with { ContinuationState = call.Input }, null);
        },
    };

    /// <summary>Ten sessions preserve two inputs/results each and two registered providers overlap.</summary>
    [Fact]
    public async Task RoutesTwentyRequestsAcrossTenPublicSessions()
    {
        ConcurrentQueue<ProviderCall> calls = new();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider first = Provider(release.Task, calls, firstEntered);
        ControlledProvider second = Provider(release.Task, calls, secondEntered);
        InMemorySessionStore store = new();
        string directory = Path.GetTempPath();
        TestStateDirectoryLease lease = new(directory);
        await using SessionRuntime runtime = new(store, lease,
            [new("one", "controlled", 1, first), new("two", "controlled", 1, second)],
            [new("left", "one", "model-left", ExecutionKind.AgentTask, new("one", new Dictionary<string, string>())),
             new("right", "two", "model-right", ExecutionKind.AgentTask, new("two", new Dictionary<string, string>()))]);
        await runtime.StartAsync(new() { StateDirectory = directory, Retention = new(true, null), MaxConcurrentRequests = 2 }, CancellationToken.None);
        List<(SessionSnapshot Session, RequestReceipt First, RequestReceipt Second)> sessions = [];
        for (int i = 0; i < 10; i++)
        {
            SessionSnapshot session = await runtime.Sessions.CreateSessionAsync(i % 2 == 0 ? "left" : "right", new(directory, null), CancellationToken.None);
            RequestReceipt a = await runtime.Requests.SubmitAsync(new(session.SessionId, $"{i}-first", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            RequestReceipt b = await runtime.Requests.SubmitAsync(new(session.SessionId, $"{i}-second", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            sessions.Add((session, a, b));
        }
        await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, store.Reservations.Count);
        Assert.Equal(2, calls.Count);
        release.TrySetResult();
        using CancellationTokenSource testDeadline = new(TimeSpan.FromSeconds(10));
        foreach (var item in sessions)
        {
            RequestSnapshot? request;
            do
            {
                await Task.Yield();
                testDeadline.Token.ThrowIfCancellationRequested();
                request = await runtime.Requests.GetRequestAsync(item.Second.RequestId, testDeadline.Token);
            } while (request?.State != RequestState.Succeeded);
            Page<HistoryEntry> history = await runtime.Sessions.ReadHistoryAsync(item.Session.SessionId, 100, null, testDeadline.Token);
            Assert.Equal(new[] { item.First.RequestId, item.Second.RequestId }, history.Items.Select(entry => entry.RequestId));
            Assert.Equal(new long[] { 1, 2 }, history.Items.Select(entry => entry.Sequence));
            Assert.Equal(item.Session.Context.ModelId, (await runtime.Sessions.GetSessionAsync(item.Session.SessionId, testDeadline.Token))?.Context.ModelId);
        }
        Assert.Equal(20, calls.Count);
        Assert.Empty((await runtime.Recovery.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items);
        Assert.Equal(10, (await runtime.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items.Count);
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.True(runtime.Completion.IsCompletedSuccessfully);
        await runtime.DisposeAsync();
        Assert.True(lease.IsDisposed);
    }

    /// <summary>Operations before Start are explicit lifecycle failures, not accidental null access.</summary>
    [Fact]
    public async Task RejectsOperationsOutsideStartedLifetime()
    {
        string directory = Path.GetTempPath();
        TestStateDirectoryLease lease = new(directory);
        await using SessionRuntime runtime = new(new InMemorySessionStore(), lease, [], []);
        Assert.Throws<SessionOperationException>(() => runtime.Requests);
        await runtime.StartAsync(new() { StateDirectory = directory, Retention = new(true, null) }, CancellationToken.None);
        await Assert.ThrowsAsync<SessionOperationException>(() => runtime.StartAsync(new() { StateDirectory = directory, Retention = new(true, null) }, CancellationToken.None).AsTask());
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Requests.SubmitAsync(new(Guid.NewGuid(), "input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.True(lease.IsDisposed);
    }
}
