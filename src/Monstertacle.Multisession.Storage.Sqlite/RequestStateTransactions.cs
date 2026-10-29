using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RequestStateTransactions
{
    internal static void AssertTransition(SqliteConnection connection, SqliteTransaction transaction, RequestSnapshot next)
    {
        RequestSnapshot current = QueryTransactions.Request(connection, next.RequestId, transaction)
            ?? throw new SessionOperationException(new(ErrorCode.NotFound));
        if (!RequestStateMachine.CanTransition(current.State, current.Certainty, next.State, next.Certainty))
        {
            throw new SessionOperationException(new(ErrorCode.RevisionConflict));
        }
        if (next.Result is not { } result || result.Outcome != next.State || result.Certainty != next.Certainty)
        {
            throw new SessionOperationException(new(ErrorCode.InvalidInput));
        }
    }
}
