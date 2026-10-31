using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Test-only native process boundary. Never initializes a store or starts a provider.
internal static class NativeOwnershipWorker
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || !Path.IsPathFullyQualified(args[1])
            || !Path.GetFileName(args[1]).StartsWith(OwnershipProtocol.DirectoryPrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(args[2], "N", out _) || !Guid.TryParseExact(args[3], "D", out Guid scope)
            || scope == Guid.Empty) { return 2; }
        try
        {
            string marker = Path.Combine(args[1], OwnershipProtocol.MarkerFileName);
            if (!File.Exists(marker) || new FileInfo(marker).Length != args[2].Length
                || !string.Equals(await File.ReadAllTextAsync(marker), args[2], StringComparison.Ordinal)) { return 2; }
            await using NativeLease lease = NativeLease.OpenExisting(args[1]);
            await lease.PrepareFileAsync(OwnershipProtocol.MarkerFileName, StateFileMode.OpenExisting, CancellationToken.None);
            RuntimeOptions options = new() { StateDirectory = args[1], Retention = new(true, null) };
            await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, scope, options, CancellationToken.None);
            await store.StartAsync(lease, CancellationToken.None);
            using CrashJournal journal = CrashJournal.Begin(Path.Combine(args[1], "multisession.db"));
            await SignalAsync(new(Environment.ProcessId, true, null));
            await Console.In.ReadLineAsync();
            return 0;
        }
        catch (SessionOperationException failure)
        {
            await SignalAsync(new(Environment.ProcessId, false, failure.Failure.Code));
            return 3;
        }
        catch (Exception)
        {
            await SignalAsync(new(Environment.ProcessId, false, ErrorCode.StorageUnavailable));
            return 3;
        }
    }

    private static async Task SignalAsync(OwnershipSignal signal)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(signal));
        await Console.Out.FlushAsync();
    }
}
