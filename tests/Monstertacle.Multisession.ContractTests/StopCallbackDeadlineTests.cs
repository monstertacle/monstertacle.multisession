using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Provider cancellation callbacks cannot consume the caller's bounded stop thread.</summary>
/// <param name="output">Receives the spec 008 T006 D2 observation if the callback-entry wait times out.</param>
public sealed class StopCallbackDeadlineTests(ITestOutputHelper output)
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
        CancellationToken? workerToken = null;
        ControlledProvider provider = new()
        {
            Sending = async (_, binding, _, token) =>
            {
                workerToken = token; // Published before `registered`, which the test awaits before stopping.
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
            try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException primary)
            {
                ReportCallbackEntryTimeout(primary, callbackThrows, stopping, workerToken, entered.Task.IsCompleted, output.WriteLine);
                throw;
            }
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

    // Spec 008 T006 D2, granted for a future natural failure: one line of observations at the callback-entry timeout, read
    // without waiting. It names no cause. Since the stop requests worker cancellation early, an unrequested token can also mean
    // that the stop had not yet run or reached that request, so the line does not separate the historical hypotheses H1 and H2.
    // A failing write is thrown together with the timeout, which stays first; otherwise the caller rethrows the timeout itself.
    private static void ReportCallbackEntryTimeout(TimeoutException primary, bool callbackThrows, Task stopping, CancellationToken? workerToken,
        bool callbackEntered, Action<string> write)
    {
        string stop = stopping.Status.ToString();
        if (stopping.Exception?.InnerException is SessionOperationException failure)
        {
            stop += string.Create(CultureInfo.InvariantCulture, $" code={failure.Failure.Code} certainty={failure.Failure.Certainty}");
        }
        else if (stopping.Exception?.InnerException is { } other) { stop += " failure=" + other.GetType().FullName; }
        string token = workerToken is { } captured ? "captured cancellation-requested=" + captured.IsCancellationRequested : "not-captured";
        string line = string.Create(CultureInfo.InvariantCulture,
            $"t006-d2 observation stage=callback-entry-wait-timeout callbackThrows={callbackThrows} stopping={stop} worker-token={token} callback-entered={callbackEntered} (observations only; no cause)");
        try { write(line); }
        catch (Exception report)
        {
            throw new AggregateException("Reporting the T006 callback-entry observation failed after the timeout.", primary, report);
        }
    }
}
