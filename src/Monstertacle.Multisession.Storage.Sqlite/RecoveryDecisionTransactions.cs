using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RecoveryDecisionTransactions
{
    internal static void Resolve(SqliteConnection connection, SqliteStoreState state, RecoveryDecision decision, ProviderBinding? binding, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            Validate(decision);
            RecoveryCaseSnapshot item = RecoveryCases.Read(connection, decision.Target, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
            using SqliteCommand duplicate = SqliteStatements.Create(connection, transaction, "SELECT count(*) FROM RecoveryDecisions WHERE DecisionId=$id", ("$id", decision.DecisionId.ToString("D")));
            if (item.Status != RecoveryCaseStatus.Open || item.Revision != decision.ExpectedRevision || !Equals(duplicate.ExecuteScalar(), 0L))
            {
                throw new SessionOperationException(new(ErrorCode.RevisionConflict));
            }
            SessionSnapshot session = QueryTransactions.Session(connection, item.SessionId, transaction)!;
            DateTimeOffset now = state.Clock.GetUtcNow();
            RecoveryCaseSnapshot updated = item with { Revision = checked(item.Revision + 1) };
            if (decision.Kind == RecoveryDecisionKind.CloseSession)
            {
                QueuedCancellationTransactions.CancelSession(connection, transaction, session.SessionId, now);
                SessionTransactions.Update(connection, transaction, session with { Lifecycle = SessionLifecycle.Closing, BlockReason = SessionBlockReason.RecoveryRequired, UpdatedUtc = now, ClosedUtc = null });
            }
            else
            {
                ConfirmEnd(connection, transaction, item, session, binding, now);
                updated = updated with { Status = RecoveryCaseStatus.Resolved, Certainty = ExecutionCertainty.ConfirmedEnded, BlockReason = SessionBlockReason.None };
            }
            RecoveryCases.Save(connection, transaction, updated);
            using SqliteCommand record = SqliteStatements.Create(connection, transaction,
                "INSERT INTO RecoveryDecisions (DecisionId,TargetKind,TargetId,AppliedUtc,DecisionJson) VALUES ($id,$kind,$target,$now,$json)",
                ("$id", decision.DecisionId.ToString("D")), ("$kind", (int)decision.Target.Kind), ("$target", decision.Target.Id.ToString("D")),
                ("$now", StoreSerialization.Utc(now)), ("$json", StoreSerialization.Write(decision)));
            record.ExecuteNonQuery();
            return true;
        }, token);

    private static void Validate(RecoveryDecision decision)
    {
        if (decision.DecisionId == Guid.Empty || decision.ExpectedRevision < 1 || !Enum.IsDefined(decision.Kind)
            || string.IsNullOrWhiteSpace(decision.Explanation) || string.IsNullOrWhiteSpace(decision.EvidenceReference))
        {
            throw new SessionOperationException(new(ErrorCode.InvalidInput));
        }
    }

    private static void ConfirmEnd(SqliteConnection connection, SqliteTransaction transaction, RecoveryCaseSnapshot item, SessionSnapshot session, ProviderBinding? binding, DateTimeOffset now)
    {
        if (session.Lifecycle != SessionLifecycle.Closing && !Compatible(item.Binding, binding)) { throw new SessionOperationException(new(ErrorCode.ProviderUnavailable)); }
        if (item.RequestId is { } requestId) { ConfirmRequest(connection, transaction, requestId); }
        if (item.DispatchId is { } dispatchId)
        {
            using SqliteCommand release = SqliteStatements.Create(connection, transaction,
                "UPDATE Reservations SET ReleasedUtc=$now,ReleaseReason=$reason WHERE DispatchId=$id AND ReleasedUtc IS NULL",
                ("$now", StoreSerialization.Utc(now)), ("$reason", RecoveryDecisionKind.ConfirmEnded.ToString()), ("$id", dispatchId.ToString("D")));
            release.ExecuteNonQuery();
        }
        bool otherOpen = RecoveryCases.OtherOpen(connection, transaction, session.SessionId, item.Target);
        using SqliteCommand remaining = SqliteStatements.Create(connection, transaction,
            "SELECT (SELECT count(*) FROM Requests WHERE SessionId=$id AND State IN ($queued,$running))+(SELECT count(*) FROM Reservations WHERE SessionId=$id AND ReleasedUtc IS NULL)",
            ("$id", session.SessionId.ToString("D")), ("$queued", (int)RequestState.Queued), ("$running", (int)RequestState.Running));
        bool pending = !Equals(remaining.ExecuteScalar(), 0L);
        SessionLifecycle lifecycle = session.Lifecycle == SessionLifecycle.Closing
            ? (otherOpen || pending ? SessionLifecycle.Closing : SessionLifecycle.Closed) : SessionLifecycle.Open;
        SessionTransactions.Update(connection, transaction, session with
        {
            Lifecycle = lifecycle,
            BlockReason = otherOpen ? SessionBlockReason.RecoveryRequired : SessionBlockReason.None,
            Binding = binding ?? session.Binding,
            BindingRevision = binding is null ? session.BindingRevision : checked(session.BindingRevision + 1),
            UpdatedUtc = now,
            ClosedUtc = lifecycle == SessionLifecycle.Closed ? now : null,
        });
    }

    private static void ConfirmRequest(SqliteConnection connection, SqliteTransaction transaction, Guid requestId)
    {
        RequestSnapshot request = QueryTransactions.Request(connection, requestId, transaction)!;
        ProviderReply? result = request.Result is null ? null : request.Result with
        {
            Certainty = ExecutionCertainty.ConfirmedEnded,
            Error = request.Result.Error is null ? null : request.Result.Error with { Certainty = ExecutionCertainty.ConfirmedEnded },
        };
        RequestSnapshot updated = request with { Certainty = ExecutionCertainty.ConfirmedEnded, Result = result };
        RequestStateTransactions.AssertTransition(connection, transaction, updated);
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "UPDATE Requests SET Certainty=$certainty,SnapshotJson=$snapshot WHERE RequestId=$id; UPDATE Results SET ReplyJson=$reply WHERE RequestId=$id;",
            ("$certainty", (int)ExecutionCertainty.ConfirmedEnded), ("$snapshot", StoreSerialization.Write(updated)), ("$id", requestId.ToString("D")), ("$reply", StoreSerialization.Write(result)));
        command.ExecuteNonQuery();
    }

    private static bool Compatible(ProviderBinding? original, ProviderBinding? reopened)
        => original?.SessionReference is not null && reopened is not null
            && original.AdapterId == reopened.AdapterId && original.FormatVersion == reopened.FormatVersion && original.SessionReference == reopened.SessionReference;
}
