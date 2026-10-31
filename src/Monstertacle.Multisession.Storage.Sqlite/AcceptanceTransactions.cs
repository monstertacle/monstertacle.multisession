using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class AcceptanceTransactions
{
    internal static ValueTask<RequestReceipt> CommitAsync(SqliteStoreState state, RequestAcceptance acceptance, int maximumQueued, CancellationToken token)
        => new(state.Queue.ExecuteAsync(connection => Commit(connection, state, acceptance, maximumQueued, token), token, true));

    private static RequestReceipt Commit(SqliteConnection connection, SqliteStoreState state, RequestAcceptance acceptance, int maximumQueued, CancellationToken token)
    {
        state.RequireAcceptance();
        return SqliteTransactions.Run(connection, transaction =>
        {
            SubmitRequest request = acceptance.Submission;
            SessionSnapshot? session = QueryTransactions.Session(connection, request.SessionId, transaction);
            if (RequestSubmissionRules.Validate(request, session, state.Options) is { } failure) { throw new SessionOperationException(failure); }
            if (acceptance.RequestId == Guid.Empty || maximumQueued <= 0) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
            using SqliteCommand duplicate = SqliteStatements.Create(connection, transaction, "SELECT count(*) FROM Requests WHERE RequestId=$id", ("$id", acceptance.RequestId.ToString("D")));
            if (!Equals(duplicate.ExecuteScalar(), 0L)) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
            using SqliteCommand capacity = SqliteStatements.Create(connection, transaction, "SELECT count(*) FROM Requests WHERE State=$queued", ("$queued", (int)RequestState.Queued));
            if ((long)capacity.ExecuteScalar()! >= maximumQueued) { throw new SessionOperationException(new(ErrorCode.QueueFull)); }
            return Insert(connection, state, transaction, acceptance, session!);
        }, token, true);
    }

    private static RequestReceipt Insert(SqliteConnection connection, SqliteStoreState state, SqliteTransaction transaction, RequestAcceptance acceptance, SessionSnapshot session)
    {
        SubmitRequest submission = acceptance.Submission;
        using SqliteCommand next = SqliteStatements.Create(connection, transaction, "SELECT NextSequence FROM Sessions WHERE SessionId=$id", ("$id", submission.SessionId.ToString("D")));
        long sequence = (long)next.ExecuteScalar()!;
        RequestSnapshot snapshot = new(acceptance.RequestId, submission.SessionId, sequence, RequestState.Queued, ExecutionCertainty.NotStarted, null)
        {
            AcceptedUtc = state.Clock.GetUtcNow(),
            SubmissionFingerprint = RequestSubmissionFingerprint.Compute(submission),
        };
        SessionSnapshot updated = session with { UpdatedUtc = snapshot.AcceptedUtc };
        using SqliteCommand insert = SqliteStatements.Create(connection, transaction,
            """
            INSERT INTO Requests (RequestId,SessionId,Sequence,AcceptedUtc,State,Certainty,SubmissionJson,SnapshotJson)
            VALUES ($id,$session,$sequence,$accepted,$state,$certainty,$input,$snapshot);
            UPDATE Sessions SET NextSequence=$next,UpdatedUtc=$accepted,SnapshotJson=$sessionSnapshot WHERE SessionId=$session;
            """, ("$id", acceptance.RequestId.ToString("D")), ("$session", submission.SessionId.ToString("D")), ("$sequence", sequence),
            ("$accepted", StoreSerialization.Utc(snapshot.AcceptedUtc)), ("$state", (int)snapshot.State), ("$certainty", (int)snapshot.Certainty),
            ("$input", StoreSerialization.Write(submission)), ("$snapshot", StoreSerialization.Write(snapshot)),
            ("$sessionSnapshot", StoreSerialization.Write(updated)), ("$next", checked(sequence + 1)));
        insert.ExecuteNonQuery();
        return new(acceptance.RequestId, sequence);
    }
}
