using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Creates an actual uncommitted SQLite journal only inside an explicitly marked test directory.
internal static class CrashWorker
{
    private static readonly JsonSerializerOptions _camelCase = new(JsonSerializerDefaults.Web);

    internal static async Task<int> RunAsync(string[] args, bool native = false)
    {
        if (args.Length != 4 || !CrashProtocol.IsKnown(args[3]) || !Path.IsPathFullyQualified(args[1])
            || !Path.GetFileName(args[1]).StartsWith("multisession-", StringComparison.Ordinal)
            || !Guid.TryParseExact(args[2], "N", out _)) { return 2; }
        string marker = Path.Combine(args[1], CrashProtocol.MarkerFileName);
        if (!File.Exists(marker) || new FileInfo(marker).Length != args[2].Length
            || !string.Equals(await File.ReadAllTextAsync(marker), args[2], StringComparison.Ordinal)) { return 2; }
        string path = Path.Combine(args[1], "multisession.db");
        CrashStepClock clock = new();
        try
        {
            if (args[3] != CrashProtocol.JournalPoint)
            {
                await using IStateDirectoryLease lease = native ? NativeLease.OpenExisting(args[1]) : new CrashStateDirectoryLease(args[1]);
                return await CrashRecoveryWorker.RunAsync(lease, args[3], clock);
            }
            using CrashJournal journal = CrashJournal.Begin(path);
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new CrashSignal(CrashProtocol.JournalBarrier, Environment.ProcessId, DateTimeOffset.UtcNow)));
            await Console.Out.FlushAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 2;
        }
        catch (Exception failure)
        {
            // The line keeps the stage, acceptance status, candidate and step, so an uncertain candidate can be looked up later.
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(CrashFailure.From(failure, clock), _camelCase));
            return 2;
        }
    }
}
