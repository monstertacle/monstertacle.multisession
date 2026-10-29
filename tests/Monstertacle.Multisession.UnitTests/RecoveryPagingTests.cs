using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks opening/execution key order and continuation after resolving the last read case.</summary>
public sealed class RecoveryPagingTests
{
    private static async Task OpeningAsync(InMemorySessionStore store)
    {
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening, Binding = null };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
    }

    /// <summary>Recovery ordering includes target kind; resolving an earlier case does not shift an offset.</summary>
    [Fact]
    public async Task ContinuesCasesAfterResolutionWithStableKindOrdering()
    {
        ManualTimeProvider clock = new();
        InMemorySessionStore store = new() { Clock = clock };
        await OpeningAsync(store);
        SessionSnapshot execution = SchedulingData.Session();
        await store.CreateSessionAsync(execution, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(execution.SessionId, "input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        await OpeningAsync(store);
        RecoveryService recovery = new(store, [], RuntimeOptionsTests.ValidOptions());
        Page<RecoveryCaseSnapshot> first = await recovery.ListRecoveryCasesAsync(1, null, CancellationToken.None);
        RecoveryCaseSnapshot before = Assert.Single(first.Items);
        Assert.Equal(RecoveryTargetKind.SessionOpening, before.Target.Kind);
        Assert.NotNull(first.NextCursor);
        await recovery.ResolveRecoveryAsync(new(Guid.NewGuid(), before.Target, before.Revision, RecoveryDecisionKind.CloseSession, "close after investigation", "reference"), CancellationToken.None);
        await recovery.ResolveRecoveryAsync(new(Guid.NewGuid(), before.Target, before.Revision + 1, RecoveryDecisionKind.ConfirmEnded, "operator confirmed end", "reference"), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));
        await OpeningAsync(store);
        Page<RecoveryCaseSnapshot> rest = await recovery.ListRecoveryCasesAsync(100, first.NextCursor, CancellationToken.None);
        Assert.Equal(new[] { RecoveryTargetKind.SessionOpening, RecoveryTargetKind.RequestExecution, RecoveryTargetKind.SessionOpening }, rest.Items.Select(item => item.Target.Kind));
        Assert.Null(rest.NextCursor);
        Assert.Equal(RecoveryCaseStatus.Open, before.Status);
        Assert.Equal(RecoveryCaseStatus.Resolved, (await recovery.GetRecoveryCaseAsync(before.Target, CancellationToken.None))?.Status);
        Assert.Single(store.Reservations);
    }
}
