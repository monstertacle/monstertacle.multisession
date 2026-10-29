using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks local cancellation visibility and persistence with a real runtime/store.</summary>
public sealed class LifecyclePersistenceTests(ITestOutputHelper output)
{
    /// <summary>A held uncooperative execution becomes durably Cancelled/Unknown within two seconds of intent.</summary>
    [Fact]
    public async Task PublishesRunningCancelWithinTwoSecondsAndRetainsReservation()
    {
        await using AdmissionStoreFixture setup = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await setup.CreateSessionAsync();
        await setup.Store.DisposeAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        AdmissionProvider provider = new()
        {
            Sending = async (call, binding, observe, token) => { observed = token; entered.TrySetResult(); return await finish.Task; },
        };
        RequestSnapshot cancelled;
        await using (SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(setup.Directory, [new("connection-I", "fixture-adapter", 7, provider)]))
        {
            RequestReceipt receipt = await host.Runtime.Requests.SubmitAsync(new(session.SessionId, "held input", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                long began = Stopwatch.GetTimestamp();
                await host.Runtime.Requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
                cancelled = await host.CompletedAsync(receipt.RequestId);
                TimeSpan elapsed = Stopwatch.GetElapsedTime(began);
                output.WriteLine(FormattableString.Invariant($"Cancel-to-durable-terminal: {elapsed.TotalMilliseconds:F3} ms"));
                Assert.True(elapsed < TimeSpan.FromSeconds(2));
                Assert.True(observed.IsCancellationRequested);
                Assert.Equal(RequestState.Cancelled, cancelled.State);
                Assert.Equal(ExecutionCertainty.Unknown, cancelled.Certainty);
                Assert.NotNull(cancelled.CancellationRequestedUtc);
                Assert.Equal(1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
            }
            finally { finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, session.Binding, null)); }
        }
        await setup.ReopenAsync();
        Assert.Equal(cancelled, await setup.Store.GetRequestAsync(cancelled.RequestId, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(setup.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
