using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Valid recovery histories remain readable despite later certainty and continuation changes.</summary>
public sealed class RecoveryGraphConsistencyRegressionTests
{
    /// <summary>Repeated close decisions and a later confirmation are ordered by case revisions rather than wallclock timestamps.</summary>
    [Fact]
    public async Task AcceptsMultipleCloseDecisionsAndNonmonotonicAppliedTimes()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryTarget target = new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId);
        List<Guid> decisions = [];
        for (int index = 0; index < 3; index++)
        {
            RecoveryCaseSnapshot item = (await fixture.Host.Store.GetRecoveryCaseAsync(target, CancellationToken.None))!;
            RecoveryDecisionKind kind = index == 2 ? RecoveryDecisionKind.ConfirmEnded : RecoveryDecisionKind.CloseSession;
            Guid identity = Guid.NewGuid();
            decisions.Add(identity);
            await fixture.Host.Store.ResolveRecoveryAsync(new(identity, target, item.Revision, kind, "explicit decision", "non-secret evidence"), null, CancellationToken.None);
        }
        await fixture.Host.Store.DisposeAsync();
        for (int index = 0; index < decisions.Count; index++)
        {
            string applied = new DateTimeOffset(2026 - index, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
            StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE RecoveryDecisions SET AppliedUtc=$time WHERE DecisionId=$id", ("$time", applied), ("$id", decisions[index].ToString("D")));
        }
        await fixture.Host.ReopenAsync();
        Assert.Equal(RecoveryCaseStatus.Resolved, (await fixture.Host.Store.GetRecoveryCaseAsync(target, CancellationToken.None))!.Status);
        Assert.Equal(SessionLifecycle.Closed, (await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))!.Lifecycle);
        Assert.Equal(RequestState.Interrupted, (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!.State);
    }

    /// <summary>A resolved opening case retains its old continuation head after later successful turns.</summary>
    [Fact]
    public async Task AcceptsHistoricalOpeningBindingAfterContinuationAdvances()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        RecoveryTarget target = new(RecoveryTargetKind.SessionOpening, session.SessionId);
        RecoveryCaseSnapshot item = (await fixture.Store.GetRecoveryCaseAsync(target, CancellationToken.None))!;
        await fixture.Store.ResolveRecoveryAsync(new(Guid.NewGuid(), target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "opening end confirmed", "non-secret evidence"), session.Binding, CancellationToken.None);
        await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "later turn", TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
        ExecutionReservation reservation = (await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None))!;
        await fixture.Store.CommitCompletionAsync(reservation.DispatchId, new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded,
            "later output", true, session.Binding! with { ContinuationState = "later-head" }, null), CancellationToken.None);
        await fixture.ReopenAsync();
        Assert.Equal(session.Binding, (await fixture.Store.GetRecoveryCaseAsync(target, CancellationToken.None))!.Binding);
        Assert.Equal("later-head", (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))!.Binding!.ContinuationState);
    }
}
