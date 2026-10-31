using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>An independent consumer-owned adapter composes only public library APIs.</summary>
public sealed class EmbeddedConsumerTests
{
    private static readonly string[] _headSequence = ["0", "1"];
    private static readonly string[] _secondSessionInputs = ["first-b", "follow-b"];
    /// <summary>Two sessions overlap, retain their own heads, accept follow-ups, cancel cooperatively and stop without deleting data.</summary>
    [Fact]
    public async Task RunsCompletePublicEmbeddingFlowWithoutLibraryChangesOrServerProcess()
    {
        await using PrivateStoreDirectory directory = new();
        RuntimeOptions options = directory.Options with { MaxConcurrentRequests = 2 };
        ConsumerOwnedProvider provider = new();
        await using RetainedTestLease lease = new(directory);
        ISessionStore store = await SqliteStoreFactory.CreateNewAsync(lease, directory.TrustScopeId, options, CancellationToken.None);
        await using SessionRuntime runtime = new(store, lease, [new("consumer", "consumer-owned", 1, provider)],
            [new("profile", "consumer", "opaque-consumer-model", ExecutionKind.ModelResponse, new("frozen", new Dictionary<string, string>()))]);
        await runtime.StartAsync(options, CancellationToken.None);
        SessionSnapshot a = await runtime.Sessions.CreateSessionAsync("profile", new(null, null), CancellationToken.None);
        SessionSnapshot b = await runtime.Sessions.CreateSessionAsync("profile", new(null, null), CancellationToken.None);
        RequestReceipt firstA = await SubmitAsync(runtime, a.SessionId, "  first-a α🙂\r\n");
        RequestReceipt firstB = await SubmitAsync(runtime, b.SessionId, "first-b");
        try
        {
            await provider.BothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(RequestState.Running, (await runtime.Requests.GetRequestAsync(firstA.RequestId, CancellationToken.None))?.State);
            Assert.Equal(RequestState.Running, (await runtime.Requests.GetRequestAsync(firstB.RequestId, CancellationToken.None))?.State);
        }
        finally { provider.ReleaseFirsts(); }
        Assert.Equal("consumer:  first-a α🙂\r\n", (await EndAsync(runtime, firstA.RequestId)).Result?.Output);
        Assert.Equal("consumer:first-b", (await EndAsync(runtime, firstB.RequestId)).Result?.Output);
        RequestReceipt followA = await SubmitAsync(runtime, a.SessionId, "follow-a");
        RequestReceipt followB = await SubmitAsync(runtime, b.SessionId, "follow-b");
        Assert.Equal(RequestState.Succeeded, (await EndAsync(runtime, followA.RequestId)).State);
        Assert.Equal(RequestState.Succeeded, (await EndAsync(runtime, followB.RequestId)).State);
        Assert.Equal(_headSequence, provider.Inputs.Where(item => item.SessionId == a.SessionId).Select(item => item.PreviousHead));
        RequestReceipt delayed = await SubmitAsync(runtime, a.SessionId, "delayed");
        Assert.Equal(delayed.RequestId, await provider.DelayedEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        long cancellation = Stopwatch.GetTimestamp();
        await runtime.Requests.CancelRequestAsync(delayed.RequestId, CancellationToken.None);
        RequestSnapshot cancelled = await EndAsync(runtime, delayed.RequestId);
        Assert.True(Stopwatch.GetElapsedTime(cancellation) < TimeSpan.FromSeconds(2));
        Assert.Equal(RequestState.Cancelled, cancelled.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, cancelled.Certainty);
        Assert.Equal(5, provider.Inputs.Count);
        Assert.Equal(_secondSessionInputs, (await runtime.Sessions.ReadHistoryAsync(b.SessionId, 100, null, CancellationToken.None)).Items.Select(item => item.Input));
        await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.True(runtime.Completion.IsCompletedSuccessfully);
        Assert.False(lease.IsHeld);
        Assert.True(File.Exists(directory.DatabasePath));
        await using RetainedTestLease reopenedLease = new(directory);
        await using ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(reopenedLease, directory.TrustScopeId, options, CancellationToken.None);
        await reopened.StartAsync(reopenedLease, CancellationToken.None);
        foreach (Guid id in new[] { firstA.RequestId, firstB.RequestId, followA.RequestId, followB.RequestId, delayed.RequestId })
        {
            Assert.NotNull(await reopened.GetRequestAsync(id, CancellationToken.None));
        }
        Assert.Equal(0, (await reopened.GetRuntimeStatisticsAsync(CancellationToken.None)).OccupiedReservations);
    }
    private static ValueTask<RequestReceipt> SubmitAsync(SessionRuntime runtime, Guid session, string input)
        => runtime.Requests.SubmitAsync(new(session, input, TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
    private static async Task<RequestSnapshot> EndAsync(SessionRuntime runtime, Guid request)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            RequestSnapshot current = (await runtime.Requests.GetRequestAsync(request, deadline.Token))!;
            if (current.State is not (RequestState.Queued or RequestState.Running)) { return current; }
            await Task.Delay(10, deadline.Token);
        }
    }
}
