using System.Buffers.Binary;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqliteStorePreflight
{
    private const int _headerLength = 100;
    private const int _userVersionOffset = 60;
    private const int _applicationIdOffset = 68;
    private const int _writeVersionOffset = 18;
    private const int _readVersionOffset = 19;
    private const byte _rollbackFormatVersion = 1;
    private const string _journalSuffix = "-journal";

    internal static async Task ValidateAsync(string path, IStateDirectoryLease lease, Guid trustScopeId, CancellationToken cancellationToken)
    {
        byte[] header = new byte[_headerLength];
        await using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, _headerLength, FileOptions.Asynchronous))
        {
            if (await stream.ReadAtLeastAsync(header, _headerLength, false, cancellationToken) != _headerLength
                || !header.AsSpan(0, "SQLite format 3\0"u8.Length).SequenceEqual("SQLite format 3\0"u8)
                || header[_writeVersionOffset] != _rollbackFormatVersion || header[_readVersionOffset] != _rollbackFormatVersion
                || BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(_userVersionOffset)) != SqliteSchema.Version
                || BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(_applicationIdOffset)) != SqliteSchema.ApplicationId)
            {
                throw SqliteSchema.Unsupported();
            }
        }
        if (!File.Exists(path + _journalSuffix))
        {
            await Task.Run(() => ValidateDatabase(path, SqliteOpenMode.ReadOnly, trustScopeId), cancellationToken);
            return;
        }
        await lease.PrepareFileAsync(Path.GetFileName(path) + _journalSuffix, StateFileMode.OpenExisting, cancellationToken);
        await ValidateJournalCopyAsync(path, lease, trustScopeId, cancellationToken);
    }

    private static async Task ValidateJournalCopyAsync(string path, IStateDirectoryLease lease, Guid trustScopeId, CancellationToken cancellationToken)
    {
        string name = ".format-check-" + Guid.NewGuid().ToString("N") + ".db";
        string copy = Path.Combine(lease.StateDirectory, name);
        bool copiedDatabase = false;
        bool copiedJournal = false;
        try
        {
            await lease.PrepareFileAsync(name, StateFileMode.CreateNew, cancellationToken);
            copiedDatabase = true;
            await CopyAsync(path, copy, cancellationToken);
            await lease.PrepareFileAsync(name + _journalSuffix, StateFileMode.CreateNew, cancellationToken);
            copiedJournal = true;
            await CopyAsync(path + _journalSuffix, copy + _journalSuffix, cancellationToken);
            await Task.Run(() => ValidateDatabase(copy, SqliteOpenMode.ReadWrite, trustScopeId), cancellationToken);
        }
        finally
        {
            if (copiedJournal) { File.Delete(copy + _journalSuffix); }
            if (copiedDatabase) { File.Delete(copy); }
        }
    }

    private static async Task CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, _headerLength, FileOptions.Asynchronous);
        await using FileStream output = new(destination, FileMode.Open, FileAccess.Write, FileShare.None, _headerLength, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static void ValidateDatabase(string path, SqliteOpenMode mode, Guid trustScopeId)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionPolicy.Open(path, mode);
            SqliteSchema.Validate(connection, trustScopeId);
        }
        catch (Exception failure) when (failure is SqliteException or InvalidCastException or FormatException)
        {
            throw SqliteSchema.Unsupported();
        }
    }
}
