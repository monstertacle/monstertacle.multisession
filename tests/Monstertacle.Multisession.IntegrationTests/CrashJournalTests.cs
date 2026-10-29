using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Monstertacle.Multisession.ValidationHost;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks the existing preflight against real spilled SQLite pages and a killed process.</summary>
[Collection("Exclusive storage volume")]
public sealed class CrashJournalTests
{
    private static readonly byte[] _hotJournalMagic = [0xD9, 0xD5, 0x05, 0xF9, 0x20, 0xA1, 0x63, 0xD7];

    /// <summary>Successful rollback preserves committed format; rejection preserves both original crash files.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlesActualHotJournalAfterForcedProcessEnd(bool unsupported)
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        object? storeId = StoreProbe.Scalar(directory.DatabasePath, "SELECT StoreId FROM Metadata");
        if (unsupported) { StoreProbe.Execute(directory.DatabasePath, "UPDATE Metadata SET SchemaVersion=2"); }
        await using CrashHarness child = await CrashHarness.StartAsync(directory, CrashProtocol.JournalPoint);
        Assert.NotNull(child.Signal);
        Assert.Equal(TimeSpan.Zero, child.Signal.Utc.Offset);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(directory.StateDirectory, CrashProtocol.MarkerFileName)));
        }
        await child.KillAsync();
        string journal = directory.DatabasePath + "-journal";
        byte[] beforeJournal = await File.ReadAllBytesAsync(journal);
        Assert.True(beforeJournal.Length > _hotJournalMagic.Length);
        Assert.Equal(_hotJournalMagic, beforeJournal[.._hotJournalMagic.Length]);
        byte[] before = await File.ReadAllBytesAsync(directory.DatabasePath);
        if (unsupported)
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
                SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.UnsupportedStoreVersion, failure.Failure.Code);
            Assert.Equal(before, await File.ReadAllBytesAsync(directory.DatabasePath));
            Assert.Equal(beforeJournal, await File.ReadAllBytesAsync(journal));
        }
        else
        {
            await (await SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
            Assert.Equal(storeId, StoreProbe.Scalar(directory.DatabasePath, "SELECT StoreId FROM Metadata"));
            Assert.Equal(0L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM sqlite_schema WHERE name='UncommittedProbe'"));
            Assert.False(File.Exists(journal));
        }
        Assert.Empty(Directory.GetFiles(directory.StateDirectory, ".format-check-*"));
    }
}
