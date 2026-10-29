using System.Text.Json;

namespace Monstertacle.Multisession.ValidationHost;

// Creates an actual uncommitted SQLite journal only inside an explicitly marked test directory.
internal static class CrashWorker
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || !CrashProtocol.IsKnown(args[3]) || !Path.IsPathFullyQualified(args[1])
            || !Path.GetFileName(args[1]).StartsWith("multisession-", StringComparison.Ordinal)
            || !Guid.TryParseExact(args[2], "N", out _)) { return 2; }
        string marker = Path.Combine(args[1], CrashProtocol.MarkerFileName);
        if (!File.Exists(marker) || new FileInfo(marker).Length != args[2].Length
            || !string.Equals(await File.ReadAllTextAsync(marker), args[2], StringComparison.Ordinal)) { return 2; }
        string path = Path.Combine(args[1], "multisession.db");
        try
        {
            if (args[3] != CrashProtocol.JournalPoint) { return await CrashRecoveryWorker.RunAsync(args[1], args[3]); }
            using CrashJournal journal = CrashJournal.Begin(path);
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
