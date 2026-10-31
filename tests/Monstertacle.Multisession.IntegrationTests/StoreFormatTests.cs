using System.Security.Cryptography;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Exercises actual SQLite files through explicit public initialization and reopening.</summary>
public sealed class StoreFormatTests
{
    /// <summary>Creates the complete supported schema and persistent identities without publishing a runtime claim.</summary>
    [Fact]
    public async Task ExplicitlyInitializesSupportedStore()
    {
        await using PrivateStoreDirectory directory = new();
        await using (ISessionStore store = await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None))
        {
            Assert.True(directory.IsHeld);
        }
        Assert.Equal(2L, StoreProbe.Scalar(directory.DatabasePath, "SELECT SchemaVersion FROM Metadata"));
        Assert.Equal(0L, StoreProbe.Scalar(directory.DatabasePath, "SELECT MigrationVersion FROM Metadata"));
        Assert.Equal(1L, StoreProbe.Scalar(directory.DatabasePath, "SELECT BindingEncodingVersion FROM Metadata"));
        Assert.Equal(directory.TrustScopeId.ToString("D"), StoreProbe.Scalar(directory.DatabasePath, "SELECT InstanceTrustScopeId FROM Metadata"));
        Assert.True(Guid.TryParse((string?)StoreProbe.Scalar(directory.DatabasePath, "SELECT StoreId FROM Metadata"), out Guid id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(8L, StoreProbe.Scalar(directory.DatabasePath, "SELECT count(*) FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%'"));
        Assert.Equal("delete", StoreProbe.Scalar(directory.DatabasePath, "PRAGMA journal_mode"));
        Assert.True(directory.IsHeld);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(directory.DatabasePath));
        }
    }

    /// <summary>Reopening keeps the original identity and data byte-for-byte when no recovery is needed.</summary>
    [Fact]
    public async Task ReopensKnownStoreWithoutReplacement()
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(directory.DatabasePath));
        object? id = StoreProbe.Scalar(directory.DatabasePath, "SELECT StoreId FROM Metadata");
        await (await SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(directory.DatabasePath)));
        Assert.Equal(id, StoreProbe.Scalar(directory.DatabasePath, "SELECT StoreId FROM Metadata"));
        Assert.True(directory.IsHeld);
    }

    /// <summary>An explicitly new store cannot overwrite an existing database.</summary>
    [Fact]
    public async Task RefusesCreateOverExistingFile()
    {
        await using PrivateStoreDirectory directory = new();
        await File.WriteAllTextAsync(directory.DatabasePath, "existing bytes must survive");
        byte[] before = await File.ReadAllBytesAsync(directory.DatabasePath);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(before, await File.ReadAllBytesAsync(directory.DatabasePath));
    }

    /// <summary>A missing expected store is never created implicitly.</summary>
    [Fact]
    public async Task RefusesMissingExpectedStore()
    {
        await using PrivateStoreDirectory directory = new();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.False(File.Exists(directory.DatabasePath));
        Assert.True(directory.IsHeld);
    }

    /// <summary>Unknown format, schema, metadata and binding encoding are rejected without modifying the original.</summary>
    [Theory]
    [InlineData("PRAGMA user_version=0")]
    [InlineData("PRAGMA user_version=3")]
    [InlineData("PRAGMA application_id=0")]
    [InlineData("UPDATE Metadata SET SchemaVersion=0")]
    [InlineData("UPDATE Metadata SET SchemaVersion=3")]
    [InlineData("UPDATE Metadata SET MigrationVersion=1")]
    [InlineData("UPDATE Metadata SET BindingEncodingVersion=2")]
    [InlineData("UPDATE Metadata SET StoreId='00000000-0000-0000-0000-000000000000'")]
    [InlineData("UPDATE Metadata SET CreatedUtc='not a timestamp'")]
    [InlineData("UPDATE Metadata SET InstanceTrustScopeId='00000000-0000-0000-0000-000000000000'")]
    [InlineData("DROP TABLE Metadata")]
    [InlineData("CREATE TABLE UnexpectedFormat (Value TEXT)")]
    [InlineData("DROP INDEX ActiveWorkspaceReservation")]
    public async Task RejectsUnsupportedStoreWithoutMutation(string tamper)
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        StoreProbe.Execute(directory.DatabasePath, tamper);
        byte[] before = await File.ReadAllBytesAsync(directory.DatabasePath);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedStoreVersion, failure.Failure.Code);
        Assert.Equal(before, await File.ReadAllBytesAsync(directory.DatabasePath));
        Assert.DoesNotContain(directory.StateDirectory, failure.ToString(), StringComparison.Ordinal);
        Assert.Equal("multisession.db", Path.GetFileName(Assert.Single(Directory.GetFiles(directory.StateDirectory))));
    }

    /// <summary>Garbage or a truncated database is not converted into a new empty store.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task RejectsCorruptStoreWithoutReplacement(int length)
    {
        await using PrivateStoreDirectory directory = new();
        await directory.PrepareFileAsync("multisession.db", StateFileMode.CreateNew, CancellationToken.None);
        byte[] contents = Enumerable.Repeat((byte)42, length).ToArray();
        await File.WriteAllBytesAsync(directory.DatabasePath, contents);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.UnsupportedStoreVersion, failure.Failure.Code);
        Assert.Equal(contents, await File.ReadAllBytesAsync(directory.DatabasePath));
    }

    /// <summary>Invalid ownership or configuration fails before touching a database file.</summary>
    [Theory]
    [InlineData("released")]
    [InlineData("scope")]
    [InlineData("directory")]
    [InlineData("retention")]
    [InlineData("queue")]
    public async Task ValidatesOwnershipAndConfigurationBeforeFileAccess(string invalid)
    {
        await using PrivateStoreDirectory directory = new();
        if (invalid == "released") { directory.Release(); }
        RuntimeOptions options = directory.Options;
        if (invalid == "directory") { options = options with { StateDirectory = Path.Combine(directory.StateDirectory, "other") }; }
        if (invalid == "retention") { options = options with { Retention = null }; }
        if (invalid == "queue") { options = options with { DbWorkQueueCapacity = 0 }; }
        Guid scope = invalid == "scope" ? Guid.Empty : directory.TrustScopeId;
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            SqliteStoreFactory.CreateNewAsync(directory, scope, options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, directory.PreparedFiles);
        Assert.Empty(Directory.GetFiles(directory.StateDirectory));
    }

    /// <summary>Pre-cancelled initialization does not prepare or create a file.</summary>
    [Fact]
    public async Task HonorsCancellationBeforeInitialization()
    {
        await using PrivateStoreDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, cancellation.Token).AsTask());
        Assert.Equal(0, directory.PreparedFiles);
        Assert.False(File.Exists(directory.DatabasePath));
    }

    /// <summary>A journal sidecar is inspected on a private copy; rejection cannot alter either original file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreflightsJournalSidecarWithoutReplacingOriginal(bool unsupported)
    {
        await using PrivateStoreDirectory directory = new();
        await (await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync();
        if (unsupported) { StoreProbe.Execute(directory.DatabasePath, "UPDATE Metadata SET SchemaVersion=3"); }
        string journal = directory.DatabasePath + "-journal";
        await directory.PrepareFileAsync("multisession.db-journal", StateFileMode.CreateNew, CancellationToken.None);
        await File.WriteAllBytesAsync(journal, new byte[64]);
        byte[] before = await File.ReadAllBytesAsync(directory.DatabasePath);
        byte[] beforeJournal = await File.ReadAllBytesAsync(journal);
        if (unsupported)
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
                SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.UnsupportedStoreVersion, failure.Failure.Code);
            Assert.Equal(before, await File.ReadAllBytesAsync(directory.DatabasePath));
            Assert.Equal(beforeJournal, await File.ReadAllBytesAsync(journal));
        }
        else { await (await SqliteStoreFactory.OpenExistingAsync(directory, directory.TrustScopeId, directory.Options, CancellationToken.None)).DisposeAsync(); }
        Assert.Empty(Directory.GetFiles(directory.StateDirectory, ".format-check-*"));
    }

    /// <summary>The native store cannot silently replace an unsupported busy duration with its fixed default.</summary>
    [Fact]
    public async Task RejectsUnsupportedBusyConfigurationBeforeFileAccess()
    {
        await using PrivateStoreDirectory directory = new();
        ISessionStore? unexpected = null;
        try
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(async () =>
                unexpected = await SqliteStoreFactory.CreateNewAsync(directory, directory.TrustScopeId,
                    directory.Options with { DbBusyTimeout = TimeSpan.FromSeconds(2) }, CancellationToken.None));
            Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
            Assert.Equal(0, directory.PreparedFiles);
        }
        finally { if (unexpected is not null) { await unexpected.DisposeAsync(); } }
    }
}
