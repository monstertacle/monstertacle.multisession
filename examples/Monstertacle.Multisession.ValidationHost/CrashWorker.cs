using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Creates an actual uncommitted SQLite journal only inside an explicitly marked test directory.
internal static class CrashWorker
{
    private const int _spillBytes = 1024 * 1024;

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[3] != CrashProtocol.JournalPoint || !Path.IsPathFullyQualified(args[1])
            || !Path.GetFileName(args[1]).StartsWith("multisession-", StringComparison.Ordinal)
            || !Guid.TryParseExact(args[2], "N", out _)) { return 2; }
        string marker = Path.Combine(args[1], CrashProtocol.MarkerFileName);
        if (!File.Exists(marker) || new FileInfo(marker).Length != args[2].Length
            || !string.Equals(await File.ReadAllTextAsync(marker), args[2], StringComparison.Ordinal)) { return 2; }
        string path = Path.Combine(args[1], "multisession.db");
        try
        {
            using SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = 1,
            }.ToString());
            connection.Open();
            using SqliteCommand settings = connection.CreateCommand();
            settings.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=EXTRA; PRAGMA cache_size=1; PRAGMA cache_spill=ON;";
            settings.ExecuteNonQuery();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE UncommittedProbe (Value BLOB); INSERT INTO UncommittedProbe VALUES (zeroblob($bytes));";
            command.Parameters.AddWithValue("$bytes", _spillBytes);
            command.ExecuteNonQuery();
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new CrashSignal(CrashProtocol.JournalBarrier, Environment.ProcessId, DateTimeOffset.UtcNow)));
            await Console.Out.FlushAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 2;
        }
        catch (Exception)
        {
            await Console.Out.WriteLineAsync("{\"status\":\"failed\",\"failureCode\":\"crash-fixture\"}");
            return 2;
        }
    }
}
