using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Provider cancellation callbacks cannot consume the caller's bounded stop thread.</summary>
public sealed class StopCallbackDeadlineTests
{
    /// <summary>A held callback keeps ownership but does not prevent reporting the stop deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsDeadlineWhileCancellationCallbackIsBlocked(bool callbackThrows)
    {
        TaskCompletionSource registered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new()
        {
            Sending = async (_, binding, _, token) =>
            {
                using CancellationTokenRegistration registration = token.Register(() =>
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                    if (callbackThrows) { throw new InvalidOperationException("private callback payload"); }
                });
                registered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { }
                return new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, null);
            },
        };
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false), CancellationToken.None);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task stopping = Task.Run(async () => await runtime.StopAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.True(lease.IsHeld);
        }
        finally
        {
            release.TrySetResult();
            await Record.ExceptionAsync(() => stopping.WaitAsync(TimeSpan.FromSeconds(5)));
            await Record.ExceptionAsync(() => runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask());
            await runtime.DisposeAsync();
        }
        Assert.True(lease.IsDisposed);
    }
}
