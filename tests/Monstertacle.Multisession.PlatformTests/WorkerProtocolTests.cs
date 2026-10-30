using System.Text.Json;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Restores process console state and runs separately from every other native test collection.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkerConsoleIsolation
{
    /// <summary>The collection that exclusively owns temporary console redirection.</summary>
    public const string Name = "native-worker-console";
}

/// <summary>Exercises real worker preparation, protocol loss and native lease release without replacing crash tests.</summary>
[Collection(WorkerConsoleIsolation.Name)]
public sealed class WorkerProtocolTests
{
    /// <summary>Unavailable protocol output preserves prepared durable state and never transfers the caller's lease.</summary>
    [Theory]
    [InlineData(CrashProtocol.BeforeAcceptance, 2)]
    [InlineData(CrashProtocol.AfterAcceptance, 3)]
    [InlineData(CrashProtocol.BeforeWake, 3)]
    [InlineData(CrashProtocol.AfterReservation, 3)]
    [InlineData(CrashProtocol.BeforeCompletion, 3)]
    [InlineData(CrashProtocol.AfterCompletion, 3)]
    public async Task SignalFailurePreservesPreparedState(string point, long expectedRequests)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            "multisession-crash-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (NativeLease lease = NativeLease.CreateNew(directory))
            {
                Guid scope = Guid.NewGuid();
                RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null) };
                await (await SqliteStoreFactory.CreateNewAsync(lease, scope, options, CancellationToken.None)).DisposeAsync();
                await lease.PrepareFileAsync(CrashProtocol.ScopeFileName, StateFileMode.CreateNew, CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(directory, CrashProtocol.ScopeFileName), scope.ToString("D"));
                await lease.PrepareFileAsync(CrashProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
                TextWriter original = Console.Out;
                StringWriter unavailable = new();
                unavailable.Dispose();
                try
                {
                    Console.SetOut(unavailable);
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => CrashRecoveryWorker.RunAsync(lease, point));
                }
                finally { Console.SetOut(original); }
                Assert.True(lease.IsHeld);
                using SqliteConnection connection = new(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(directory, "multisession.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                connection.Open();
                using SqliteCommand query = connection.CreateCommand();
                query.CommandText = "SELECT count(*) FROM Sessions";
                Assert.Equal(2L, query.ExecuteScalar());
                query.CommandText = "SELECT count(*) FROM Requests";
                Assert.Equal(expectedRequests, query.ExecuteScalar());
            }
            await using NativeLease successor = NativeLease.OpenExisting(directory);
            Assert.True(successor.IsHeld);
        }
        finally { if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); } }
    }

    /// <summary>Normal EOF rolls back the test journal; incompatible custody reports a closed error and releases ownership.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnershipWorkerReportsAndReleases(bool matchingScope)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            OwnershipProtocol.DirectoryPrefix + Guid.NewGuid().ToString("N"));
        Guid scope = Guid.NewGuid();
        string nonce = Guid.NewGuid().ToString("N");
        try
        {
            await using (NativeLease setup = NativeLease.CreateNew(directory))
            {
                RuntimeOptions options = new() { StateDirectory = directory, Retention = new(true, null) };
                await (await SqliteStoreFactory.CreateNewAsync(setup, scope, options, CancellationToken.None)).DisposeAsync();
                await setup.PrepareFileAsync(OwnershipProtocol.MarkerFileName, StateFileMode.CreateNew, CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(directory, OwnershipProtocol.MarkerFileName), nonce);
            }
            TextWriter originalOutput = Console.Out;
            TextReader originalInput = Console.In;
            using StringWriter output = new();
            using StringReader input = new(string.Empty);
            int exit;
            try
            {
                Console.SetOut(output);
                Console.SetIn(input);
                exit = await NativeOwnershipWorker.RunAsync([OwnershipProtocol.WorkerArgument, directory, nonce,
                    (matchingScope ? scope : Guid.NewGuid()).ToString("D")]);
            }
            finally { Console.SetOut(originalOutput); Console.SetIn(originalInput); }
            Assert.Equal(matchingScope ? 0 : 3, exit);
            OwnershipSignal signal = JsonSerializer.Deserialize<OwnershipSignal>(output.ToString())!;
            Assert.Equal(Environment.ProcessId, signal.ProcessId);
            Assert.Equal(matchingScope, signal.Acquired);
            Assert.Equal(matchingScope ? null : ErrorCode.UnsupportedStoreVersion, signal.Failure);
            await using NativeLease successor = NativeLease.OpenExisting(directory);
            Assert.True(successor.IsHeld);
            using SqliteConnection connection = new(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(directory, "multisession.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using SqliteCommand query = connection.CreateCommand();
            query.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='UncommittedProbe'";
            Assert.Equal(0L, query.ExecuteScalar());
        }
        finally { if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); } }
    }
}
