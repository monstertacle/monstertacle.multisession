using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Result and history projections cannot silently lose or replace committed observations.</summary>
public sealed class ResultStorageConsistencyTests
{
    /// <summary>Startup validates the complete local finalization graph before any recovery mutation.</summary>
    [Theory]
    [InlineData("result-json")]
    [InlineData("result-output")]
    [InlineData("result-certainty")]
    [InlineData("result-missing")]
    [InlineData("history-missing")]
    [InlineData("history-duplicate")]
    [InlineData("history-sequence")]
    [InlineData("history-input")]
    [InlineData("history-result")]
    [InlineData("history-request")]
    [InlineData("history-owner")]
    [InlineData("history-id")]
    [InlineData("history-ordinal")]
    [InlineData("completed-time")]
    [InlineData("history-json")]
    [InlineData("submission-owner")]
    [InlineData("submission-input")]
    [InlineData("submission-json")]
    public async Task RejectsCommittedGraphDrift(string corruption)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        ResultStoreTampering.Apply(fixture.Host.Directory.DatabasePath, corruption);
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }

    /// <summary>A queued row cannot carry a hidden final result outside its public snapshot.</summary>
    [Fact]
    public async Task RejectsResultForNonterminalRequest()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        RequestReceipt queued = await fixture.Host.Store.CommitAcceptanceAsync(new(Guid.NewGuid(),
            new(fixture.Session.SessionId, "still queued", TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath,
            "INSERT INTO Results (RequestId,CompletedUtc,ReplyJson) SELECT $id,CompletedUtc,ReplyJson FROM Results", ("$id", queued.RequestId.ToString("D")));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }

    /// <summary>Late end confirmation changes current certainty while preserving the original historical observation.</summary>
    [Fact]
    public async Task AcceptsConfirmedCurrentResultWithUnchangedUnknownHistory()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await fixture.Host.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await fixture.Host.Store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "confirmed external end", "non-secret evidence"), fixture.Session.Binding, CancellationToken.None);
        await fixture.Host.ReopenAsync();
        RequestSnapshot current = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        HistoryEntry historical = Assert.Single((await fixture.Host.Store.ReadHistoryAsync(fixture.Session.SessionId, 10, null, CancellationToken.None)).Items);
        Assert.Equal(RequestState.Interrupted, current.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, current.Certainty);
        Assert.Equal(RequestState.Interrupted, historical.Result.Outcome);
        Assert.Equal(ExecutionCertainty.Unknown, historical.Result.Certainty);
    }
}
