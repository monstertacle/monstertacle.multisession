using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>SQLite never persists a silently replaced accepted input or successful output.</summary>
public sealed class Utf8PersistenceIntegrityTests
{
    /// <summary>Both public and direct atomic acceptance reject invalid UTF16 without consuming sequence or rows.</summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public async Task InvalidInputCannotReachSerialization(int codepoint)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        SubmitRequest request = new(session.SessionId, "SECRET-" + (char)codepoint + "-INPUT", TimeSpan.FromSeconds(5), null, false);
        SessionOperationException publicFailure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(request, CancellationToken.None).AsTask());
        SessionOperationException directFailure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), request), 100, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, publicFailure.Failure.Code);
        Assert.Equal(ErrorCode.InvalidInput, directFailure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, publicFailure.Failure.AdmissionStatus);
        Assert.Equal(AdmissionStatus.NotAccepted, directFailure.Failure.AdmissionStatus);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        await fixture.ReopenAsync();
        RequestReceipt accepted = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), request with { Input = "  valid 🙂\r\n" }), 100, CancellationToken.None);
        Assert.Equal(1, accepted.Sequence);
    }

    /// <summary>A malformed confirmed response becomes an empty local failure that survives restart exactly.</summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public async Task InvalidOutputNeverBecomesReplacedSuccess(int codepoint)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ProviderReply reply = fixture.Reply("SECRET-" + (char)codepoint + "-OUTPUT");
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, reply, CancellationToken.None);
        await fixture.Host.ReopenAsync();
        RequestSnapshot request = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Failed, request.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request.Certainty);
        Assert.Equal(ErrorCode.ExecutionFailed, request.Result?.Error?.Code);
        Assert.Empty(request.Result!.Output);
        Assert.False(request.Result.OutputComplete);
        Assert.Equal(request.Result, Assert.Single((await fixture.Host.Store.ReadHistoryAsync(fixture.Session.SessionId, 10, null, CancellationToken.None)).Items).Result);
    }
}
