using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Translates SQLite failures with a defined meaning into typed refusals; any other failure propagates unchanged.</summary>
internal static class StorageFailures
{
    private const int _busy = 5;
    private const int _locked = 6;
    private const int _corrupt = 11;
    private const int _notADatabase = 26;

    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException failure) when (failure.SqliteErrorCode is _busy or _locked)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.StorageBusy, failure);
        }
        catch (SqliteException failure) when (failure.SqliteErrorCode is _corrupt or _notADatabase)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.CheckpointCorrupt, failure);
        }
    }

    public static Task RunAsync(Func<Task> operation) => RunAsync(async () =>
    {
        await operation();
        return true;
    });
}
