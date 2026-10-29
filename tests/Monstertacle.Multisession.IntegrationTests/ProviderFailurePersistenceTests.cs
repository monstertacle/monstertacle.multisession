using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Shared completion validation persists only permitted provider error metadata.</summary>
public sealed class ProviderFailurePersistenceTests
{
    /// <summary>Direct store completion and subsequent reads cannot bypass the provider boundary.</summary>
    [Theory]
    [InlineData(ErrorCode.AuthenticationFailed, ErrorCode.AuthenticationFailed)]
    [InlineData(ErrorCode.StorageUnavailable, ErrorCode.ExecutionFailed)]
    [InlineData((ErrorCode)123456, ErrorCode.ExecutionFailed)]
    public async Task SanitizedErrorSurvivesRestartAndHistory(ErrorCode supplied, ErrorCode expected)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ProviderReply reply = fixture.Reply(string.Empty) with
        {
            Outcome = RequestState.Failed,
            OutputComplete = false,
            Error = new(supplied, ExecutionCertainty.Unknown, Guid.NewGuid(), Guid.NewGuid(), AdmissionStatus.Committed)
        };
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, reply, CancellationToken.None);
        await fixture.Host.ReopenAsync();
        RequestSnapshot request = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Failed, request.State);
        Assert.Equal(new(expected, ExecutionCertainty.ConfirmedEnded), request.Result?.Error);
        HistoryEntry history = Assert.Single((await fixture.Host.Store.ReadHistoryAsync(fixture.Session.SessionId, 10, null, CancellationToken.None)).Items);
        Assert.Equal(request.Result, history.Result);
        Assert.Equal(new(0, 0, 0), await fixture.Host.Store.GetRuntimeStatisticsAsync(CancellationToken.None));
    }
}
