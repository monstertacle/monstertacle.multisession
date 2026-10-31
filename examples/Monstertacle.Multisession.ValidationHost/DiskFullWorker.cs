using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Operates only on a copied seed in a marked small test volume, never on an existing host store.
internal static class DiskFullWorker
{
    internal static async Task<int> RunAsync(string[] args)
    {
        string stage = "arguments";
        if (args.Length != 3 || !Path.IsPathFullyQualified(args[1]) || !Path.GetFileName(args[1]).StartsWith("multisession-", StringComparison.Ordinal)
            || !Guid.TryParseExact(args[2], "N", out _)) { return 2; }
        string root = args[1];
        try
        {
            string marker = Path.Combine(root, DiskFullProtocol.MarkerFile);
            if (!File.Exists(marker) || new FileInfo(marker).Length != args[2].Length || await File.ReadAllTextAsync(marker) != args[2]) { return 2; }
            DiskFullManifest manifest = JsonSerializer.Deserialize<DiskFullManifest>(await File.ReadAllTextAsync(Path.Combine(root, DiskFullProtocol.ManifestFile)))!;
            if (!Path.IsPathFullyQualified(manifest.StateDirectory) || !Directory.Exists(manifest.StateDirectory)) { return 2; }
            if (await File.ReadAllTextAsync(Path.Combine(manifest.StateDirectory, DiskFullProtocol.MarkerFile)) != args[2]) { return 2; }
            long capacity = new DriveInfo(manifest.StateDirectory).TotalSize;
            if (capacity <= 0 || capacity > DiskFullProtocol.MaximumVolumeBytes) { return 2; }
            stage = "copy";
            File.Copy(Path.Combine(root, DiskFullProtocol.SeedFile), Path.Combine(manifest.StateDirectory, "multisession.db"), false);
            await using CrashStateDirectoryLease lease = new(manifest.StateDirectory);
            RuntimeOptions options = new() { StateDirectory = manifest.StateDirectory, Retention = new(true, null), MaxConcurrentRequests = 1 };
            await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, manifest.TrustScopeId, options, CancellationToken.None);
            FaultProbeProvider provider = new();
            await using SessionRuntime runtime = new(store, lease, [new("connection-I", "fixture-adapter", 7, provider)], []);
            stage = "start";
            await runtime.StartAsync(options, CancellationToken.None);
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
            stage = "fill";
            var (fullCode, bytes) = await BoundedVolumeFiller.FillAsync(manifest.StateDirectory, deadline.Token);
            stage = "admission";
            PublicError? failure = null;
            try { await runtime.Requests.SubmitAsync(new(manifest.SessionId, new string('x', options.MaxInputBytes), TimeSpan.FromSeconds(10), null, false), deadline.Token); }
            catch (SessionOperationException rejected) { failure = rejected.Failure; }
            stage = "release";
            File.Delete(Path.Combine(manifest.StateDirectory, BoundedVolumeFiller.FileName));
            stage = "verify";
            bool verified = failure is { Code: ErrorCode.StorageUnavailable, RequestId: not null }
                && failure.AdmissionStatus != AdmissionStatus.Committed && provider.Sends == 0
                && await store.GetRequestAsync(failure.RequestId.Value, deadline.Token) is null
                && (await store.GetRequestAsync(manifest.CompletedRequestId, deadline.Token)) is { State: RequestState.Succeeded, Result.Output: "complete output" }
                && (await store.ReadHistoryAsync(manifest.SessionId, 100, null, deadline.Token)).Items.Count == 1;
            try { await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None); }
            catch (SessionOperationException stopped) when (stopped.Failure.Code == ErrorCode.StorageUnavailable) { }
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new DiskFullReport(args[2], Environment.ProcessId, stage)
            { FullCode = fullCode, FilledBytes = bytes, Failure = failure, Verified = verified, MeasuredUtc = DateTimeOffset.UtcNow }));
            return verified ? 0 : 2;
        }
        catch (Exception)
        {
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new DiskFullReport(args[2], Environment.ProcessId, stage) { MeasuredUtc = DateTimeOffset.UtcNow }));
            return 2;
        }
    }
}
