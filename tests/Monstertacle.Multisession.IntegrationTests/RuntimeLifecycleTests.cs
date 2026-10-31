using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;
using Xunit.Abstractions;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Controlled stop is durable cancellation, whereas process crashes preserve queued work.</summary>
public sealed class RuntimeLifecycleTests(ITestOutputHelper output)
{
    /// <summary>All blocked queued requests, including a second page, are cancelled without provider access and survive reopening.</summary>
    [Fact]
    public async Task StopsAcceptanceAndFinalizesAllQueuedPages()
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync(maxQueued: 130);
        SessionSnapshot first = await setup.CreateSessionAsync();
        SessionSnapshot other = await setup.CreateSessionAsync();
        RequestAdmission requests = new(setup.Store, setup.Options);
        List<Guid> ids = [];
        for (int index = 0; index < 102; index++)
        {
            RequestReceipt receipt = await requests.SubmitAsync(Guid.NewGuid(), new(index == 101 ? other.SessionId : first.SessionId,
                "queued-" + index, TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
            ids.Add(receipt.RequestId);
        }
        await setup.Store.DisposeAsync();
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(setup.Directory, []);
        long began = Stopwatch.GetTimestamp();
        Task stopping = host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask();
        SessionOperationException rejected = await Assert.ThrowsAsync<SessionOperationException>(() => host.Runtime.Requests.SubmitAsync(
            Guid.NewGuid(), new(first.SessionId, "too late", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionClosed, rejected.Failure.Code);
        await stopping;
        TimeSpan elapsed = Stopwatch.GetElapsedTime(began);
        output.WriteLine(FormattableString.Invariant($"Stop 102 queued: {elapsed.TotalMilliseconds:F3} ms"));
        Assert.True(elapsed < TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<SessionOperationException>(() => host.Runtime.StartAsync(setup.Options, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<SessionOperationException>(() => host.Runtime.Requests.GetRequestAsync(ids[0], CancellationToken.None).AsTask());
        await setup.ReopenAsync();
        foreach (Guid id in ids)
        {
            RequestSnapshot request = (await setup.Store.GetRequestAsync(id, CancellationToken.None))!;
            Assert.Equal(RequestState.Cancelled, request.State);
            Assert.Equal(ExecutionCertainty.NotStarted, request.Certainty);
            Assert.NotNull(request.EndedUtc);
            Assert.Null(request.StartedUtc);
        }
        Assert.Equal(102L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(102L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(0L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations"));
    }

    /// <summary>An actual rollback during queued finalization must be a visible stop failure.</summary>
    [Fact]
    public async Task DoesNotClaimDurableStopWhenQueuedCancellationFails()
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await setup.CreateSessionAsync();
        RequestReceipt receipt = await new RequestAdmission(setup.Store, setup.Options).SubmitAsync(
            Guid.NewGuid(), new(session.SessionId, "must remain", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestReceipt second = await new RequestAdmission(setup.Store, setup.Options).SubmitAsync(
            Guid.NewGuid(), new(session.SessionId, "must also remain", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        await setup.Store.DisposeAsync();
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(setup.Directory, []);
        StoreProbe.Execute(setup.Directory.DatabasePath, "CREATE TRIGGER FailStop AFTER UPDATE OF CancellationRequestedUtc ON Requests WHEN NEW.Sequence=2 BEGIN SELECT RAISE(FAIL,'private-stop-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private-stop-marker", failure.ToString(), StringComparison.Ordinal);
        await Assert.ThrowsAsync<SessionOperationException>(() => host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask());
        Assert.Equal(0L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        StoreProbe.Execute(setup.Directory.DatabasePath, "DROP TRIGGER FailStop");
        await setup.ReopenAsync();
        Assert.Equal(RequestState.Queued, (await setup.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
        Assert.Equal(RequestState.Queued, (await setup.Store.GetRequestAsync(second.RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Stop cancels queued followers while only confirmed external termination releases the active slot.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreservesActiveEvidenceAndCancelsFollowers(bool confirmed)
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await setup.CreateSessionAsync();
        await setup.Store.DisposeAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int sends = 0;
        AdmissionProvider provider = new()
        {
            Capabilities = new(true, true, true, confirmed, false),
            Sending = async (call, binding, observe, token) =>
            {
                Interlocked.Increment(ref sends);
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                entered.TrySetResult();
                return await finish.Task;
            },
            Cancelling = (context, operation, token) => ValueTask.FromResult(new ProviderReply(RequestState.Cancelled,
                ExecutionCertainty.ConfirmedEnded, string.Empty, false, null, new(ErrorCode.Cancelled))),
        };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(setup.Directory, [new("connection-I", "fixture-adapter", 7, provider)]);
        RequestReceipt active = await host.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "active", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        List<Guid> followers = [];
        for (int index = 0; index < 2; index++)
        {
            RequestReceipt receipt = await host.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "follower-" + index, TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
            followers.Add(receipt.RequestId);
        }
        try
        {
            long began = Stopwatch.GetTimestamp();
            await host.Runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(began);
            output.WriteLine(FormattableString.Invariant($"Stop active confirmed={confirmed}: {elapsed.TotalMilliseconds:F3} ms"));
            Assert.True(elapsed < TimeSpan.FromSeconds(10));
        }
        finally { finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, session.Binding, null)); }
        await setup.ReopenAsync();
        RequestSnapshot ended = (await setup.Store.GetRequestAsync(active.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Cancelled, ended.State);
        Assert.Equal(confirmed ? ExecutionCertainty.ConfirmedEnded : ExecutionCertainty.Unknown, ended.Certainty);
        foreach (Guid id in followers)
        {
            RequestSnapshot request = (await setup.Store.GetRequestAsync(id, CancellationToken.None))!;
            Assert.Equal(RequestState.Cancelled, request.State);
            Assert.Equal(ExecutionCertainty.NotStarted, request.Certainty);
            Assert.Null(request.StartedUtc);
        }
        Assert.Equal(1, sends);
        Assert.Equal(3L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(3L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(confirmed ? 0L : 1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
