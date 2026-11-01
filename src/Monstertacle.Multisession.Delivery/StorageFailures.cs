using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Translates SQLite failures with a defined meaning into typed refusals; any other failure propagates unchanged.</summary>
internal static class StorageFailures
{
    private const int Busy = 5;
    private const int Locked = 6;
    private const int Corrupt = 11;
    private const int NotADatabase = 26;

    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException failure) when (failure.SqliteErrorCode is Busy or Locked)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.StorageBusy, failure);
        }
        catch (SqliteException failure) when (failure.SqliteErrorCode is Corrupt or NotADatabase)
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
