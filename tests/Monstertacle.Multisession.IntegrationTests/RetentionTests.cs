using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Explicit deletion removes only eligible local contents as one transaction.</summary>
public sealed class RetentionTests
{
    /// <summary>A closed session and all request/result/history/reservation/recovery records disappear together, while independent data survives.</summary>
    [Fact]
    public async Task DeletesClosedLocalGraphWithoutTouchingIndependentSession()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        SessionSnapshot independent = await fixture.Host.CreateSessionAsync();
        ISessionStore store = fixture.Host.Store;
        RequestReceipt otherReceipt = await new RequestAdmission(store, fixture.Host.Options).SubmitAsync(new(independent.SessionId,
            "independent preserved input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        ExecutionReservation otherReservation = (await store.TryReserveHeadAsync(4, CancellationToken.None))!;
        Assert.Equal(otherReceipt.RequestId, otherReservation.RequestId);
        await store.MarkUncertainAsync(otherReservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryTarget otherTarget = new(RecoveryTargetKind.RequestExecution, otherReceipt.RequestId);
        RecoveryCaseSnapshot otherCase = (await store.GetRecoveryCaseAsync(otherTarget, CancellationToken.None))!;
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), otherTarget, otherCase.Revision, RecoveryDecisionKind.ConfirmEnded,
            "independent end", "independent non-secret evidence"), independent.Binding, CancellationToken.None);
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        await fixture.Host.Catalog.CloseSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "confirmed external end", "non-secret evidence"), null, CancellationToken.None);
        Assert.Equal(2L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM RecoveryDecisions"));
        await fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        await fixture.Host.ReopenAsync();
        store = fixture.Host.Store;
        Assert.Null(await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None));
        Assert.Null(await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None));
        Assert.Equal(independent.SessionId, Assert.Single((await store.ListSessionsAsync(100, null, CancellationToken.None)).Items).SessionId);
        foreach (string table in new[] { "Requests", "Results", "History", "Reservations", "RecoveryCases", "RecoveryDecisions" })
        {
            Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM " + table));
        }
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, (await store.GetRequestAsync(otherReceipt.RequestId, CancellationToken.None))?.Certainty);
        Assert.Equal("independent preserved input", Assert.Single((await store.ReadHistoryAsync(independent.SessionId, 100, null, CancellationToken.None)).Items).Input);
        Assert.Equal(RecoveryCaseStatus.Resolved, (await store.GetRecoveryCaseAsync(otherTarget, CancellationToken.None))?.Status);
        SessionOperationException missing = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.NotFound, missing.Failure.Code);
    }

    /// <summary>Open and unknown work cannot be deleted just by requesting local removal.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusesActiveOrUnknownLocalWork(bool unknown)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        if (unknown)
        {
            await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
            await fixture.Host.Catalog.CloseSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        }
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionBlocked, failure.Failure.Code);
        Assert.NotNull(await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>An error after deleting child rows rolls back the complete local graph.</summary>
    [Fact]
    public async Task RollsBackPartialDeletionAndHonorsCallerCancellation()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        await fixture.Host.Catalog.CloseSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, cancelled.Token).AsTask());
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "CREATE TRIGGER FailDelete BEFORE DELETE ON Sessions BEGIN SELECT RAISE(FAIL,'private-delete-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private-delete-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.NotNull(await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations"));
    }

    /// <summary>A selected duration prevents early deletion and does not trigger automatic content removal on reopen.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HonorsSelectedRetentionDurationBeforeExplicitDeletion(bool elapsed)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync(retention: new(false, elapsed ? TimeSpan.FromTicks(1) : TimeSpan.FromDays(7)));
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Catalog.CloseSessionAsync(session.SessionId, CancellationToken.None);
        await fixture.ReopenAsync();
        Assert.NotNull(await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None));
        if (elapsed)
        {
            await fixture.Catalog.DeleteSessionAsync(session.SessionId, CancellationToken.None);
            Assert.Null(await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None));
        }
        else
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Catalog.DeleteSessionAsync(session.SessionId, CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.SessionBlocked, failure.Failure.Code);
            Assert.NotNull(await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None));
        }
    }

    /// <summary>Even a corrupt Closed marker cannot authorize deletion of still-active or uncertain work.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RechecksWorkInsteadOfTrustingClosedMarker(bool unknown)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        if (unknown) { await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None); }
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions")!)!;
        snapshot["Lifecycle"] = (int)SessionLifecycle.Closed;
        DateTimeOffset closed = DateTimeOffset.UtcNow;
        snapshot["ClosedUtc"] = closed;
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE Sessions SET Lifecycle=$closed,ClosedUtc=$time,SnapshotJson=$json",
            ("$closed", (int)SessionLifecycle.Closed), ("$time", closed.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), ("$json", snapshot.ToJsonString()));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.Catalog.DeleteSessionAsync(fixture.Session.SessionId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionBlocked, failure.Failure.Code);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
