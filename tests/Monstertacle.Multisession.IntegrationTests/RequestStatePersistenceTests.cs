using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Validates final state evidence at the durable boundary and checks competing finalizers.</summary>
public sealed class RequestStatePersistenceTests
{
    /// <summary>An invalid terminal pair cannot append history or release a reservation.</summary>
    [Fact]
    public async Task RejectsInterruptedNotStartedCompletion()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ProviderReply reply = new(RequestState.Interrupted, ExecutionCertainty.NotStarted, string.Empty, false, null, new(ErrorCode.Interrupted));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, reply, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(RequestState.Running, (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))?.State);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Queued competing known/unknown finalizers have one durable winner in either submission order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingFinalizersPublishExactlyOneOutcome(bool unknownFirst)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        Task<Exception?> Unknown() => Record.ExceptionAsync(() => store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None).AsTask());
        Task<Exception?> Known() => Record.ExceptionAsync(() => store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None).AsTask());
        Task<Exception?> first = unknownFirst ? Unknown() : Known();
        Task<Exception?> second = unknownFirst ? Known() : Unknown();
        Exception?[] failures = await Task.WhenAll(first, second);
        Assert.Single(failures, failure => failure is null);
        SessionOperationException conflict = Assert.IsType<SessionOperationException>(Assert.Single(failures, failure => failure is not null));
        Assert.Equal(ErrorCode.RevisionConflict, conflict.Failure.Code);
        RequestSnapshot request = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(unknownFirst ? RequestState.Interrupted : RequestState.Succeeded, request.State);
        Assert.Equal(unknownFirst ? ExecutionCertainty.Unknown : ExecutionCertainty.ConfirmedEnded, request.Certainty);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(unknownFirst ? 1L : 0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        await fixture.Host.ReopenAsync();
        Assert.Equal(request, await fixture.Host.Store.GetRequestAsync(request.RequestId, CancellationToken.None));
    }
}
