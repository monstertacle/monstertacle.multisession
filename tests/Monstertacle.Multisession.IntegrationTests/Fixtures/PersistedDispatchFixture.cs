using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed record PersistedDispatchFixture(AdmissionStoreFixture Host, SessionSnapshot Session, RequestReceipt Receipt, ExecutionReservation Reservation) : IAsyncDisposable
{
    internal static async Task<PersistedDispatchFixture> CreateAsync(int maximumOutputBytes = 4 * 1024 * 1024)
    {
        AdmissionStoreFixture host = await AdmissionStoreFixture.CreateAsync(maxOutputBytes: maximumOutputBytes);
        try
        {
            SessionSnapshot session = await host.CreateSessionAsync();
            RequestReceipt receipt = await host.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "  original input\n🙂\r\n", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
            ExecutionReservation reservation = (await host.Store.TryReserveHeadAsync(4, CancellationToken.None))!;
            return new(host, session, receipt, reservation);
        }
        catch { await host.DisposeAsync(); throw; }
    }
    internal ProviderReply Reply(string output = "complete output")
        => new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output, true, Session.Binding! with { ContinuationState = "new-head" }, null);
    public ValueTask DisposeAsync() => Host.DisposeAsync();
}
