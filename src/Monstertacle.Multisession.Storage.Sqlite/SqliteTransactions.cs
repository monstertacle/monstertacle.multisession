using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqliteTransactions
{
    internal static T Run<T>(SqliteConnection connection, Func<SqliteTransaction, T> action, CancellationToken token, bool admission = false)
    {
        SqliteTransaction? transaction = null;
        bool committing = false;
        try
        {
            token.ThrowIfCancellationRequested();
            transaction = connection.BeginTransaction();
            T result = action(transaction);
            token.ThrowIfCancellationRequested();
            committing = true;
            transaction.Commit();
            return result;
        }
        catch (Exception failure)
        {
            bool noWrite = transaction is null;
            if (transaction is not null && !committing)
            {
                try { transaction.Rollback(); noWrite = true; }
                catch (Exception) { noWrite = false; }
            }
            throw StorageFailurePolicy.Normalize(failure, noWrite, admission, token,
                committing ? FailureStage.StoreCommit : FailureStage.StoreTransaction);
        }
        finally { transaction?.Dispose(); }
    }
}
