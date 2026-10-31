using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Runtime closure uses cancellation evidence while keeping unrelated sessions independent.</summary>
public sealed class SessionClosureRuntimeTests
{
    /// <summary>Close cancels followers and returns Closed only with confirmed end; independent work remains eligible.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosesConfirmedExecutionAndRetainsUnknownExecution(bool confirmed)
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await setup.CreateSessionAsync();
        SessionSnapshot independent = await setup.CreateSessionAsync();
        await setup.Store.DisposeAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AdmissionProvider provider = new()
        {
            Capabilities = new(true, true, true, confirmed, false),
            Sending = async (call, binding, observe, token) =>
            {
                if (call.Input == "independent") { return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "completed", true, binding, null); }
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
        RequestReceipt queued = await host.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "queued", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
        try
        {
            Task close = host.Runtime.Sessions.CloseSessionAsync(session.SessionId, CancellationToken.None).AsTask();
            RequestReceipt other = await host.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(independent.SessionId, "independent", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
            Assert.Equal(RequestState.Succeeded, (await host.CompletedAsync(other.RequestId)).State);
            await close.WaitAsync(TimeSpan.FromSeconds(3));
            SessionSnapshot snapshot = (await host.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))!;
            Assert.Equal(confirmed ? SessionLifecycle.Closed : SessionLifecycle.Closing, snapshot.Lifecycle);
            Assert.Equal(confirmed ? SessionBlockReason.None : SessionBlockReason.RecoveryRequired, snapshot.BlockReason);
            Assert.Equal(RequestState.Cancelled, (await host.Runtime.Requests.GetRequestAsync(active.RequestId, CancellationToken.None))?.State);
            Assert.Equal(ExecutionCertainty.NotStarted, (await host.Runtime.Requests.GetRequestAsync(queued.RequestId, CancellationToken.None))?.Certainty);
            Assert.Equal(confirmed ? 0L : 1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        }
        finally { finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, session.Binding, null)); }
    }

    /// <summary>A cancelled Close caller cannot erase committed intent or prevent later automatic closure.</summary>
    [Fact]
    public async Task CompletesClosureAfterCallerAbandonsItsWait()
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await setup.CreateSessionAsync();
        await setup.Store.DisposeAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelling = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> end = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AdmissionProvider provider = new()
        {
            Capabilities = new(true, true, true, true, false),
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                entered.TrySetResult();
                return await finish.Task;
            },
            Cancelling = async (context, operation, token) => { cancelling.TrySetResult(); return await end.Task.WaitAsync(token); },
        };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(setup.Directory, [new("connection-I", "fixture-adapter", 7, provider)]);
        RequestReceipt receipt = await host.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "held", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using CancellationTokenSource caller = new();
        Task close = host.Runtime.Sessions.CloseSessionAsync(session.SessionId, caller.Token).AsTask();
        try
        {
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(3));
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => close);
            Assert.Equal(SessionLifecycle.Closing, (await host.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.Lifecycle);
            end.TrySetResult(new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, null, new(ErrorCode.Cancelled)));
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, (await host.CompletedAsync(receipt.RequestId)).Certainty);
            Assert.Equal(SessionLifecycle.Closed, (await host.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.Lifecycle);
            Assert.Equal(0L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        }
        finally
        {
            end.TrySetResult(new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, null, new(ErrorCode.Cancelled)));
            finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, session.Binding, null));
            await Record.ExceptionAsync(() => close);
        }
    }
}
