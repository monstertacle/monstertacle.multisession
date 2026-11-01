using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Anything other than the current format is refused; there is no alternate reader.</summary>
public sealed class CorruptionTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>A non database file is refused as corrupt and the lease is released.</summary>
    [Fact]
    public async Task ANonDatabaseFileIsRefusedAsCorruptAndTheLeaseIsReleased()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory.StatePath)!);
        File.WriteAllText(_directory.StatePath, new string('x', 4096));

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>Another checkpoint structure is refused.</summary>
    [Theory]
    [InlineData("CREATE TABLE checkpoint(id INTEGER PRIMARY KEY CHECK(id=1),schema INTEGER NOT NULL,instance TEXT NOT NULL,project TEXT NOT NULL,route TEXT NOT NULL,cursor INTEGER NOT NULL,pending TEXT,phase TEXT,attempts INTEGER NOT NULL DEFAULT 0,receipt TEXT,extra TEXT)")]
    [InlineData("CREATE TABLE checkpoint(id INTEGER PRIMARY KEY,schema INTEGER,instance TEXT,project TEXT,route TEXT,cursor INTEGER,pending TEXT,phase TEXT,attempts INTEGER,receipt TEXT)")]
    public async Task AnotherCheckpointStructureIsRefused(string ddl)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory.StatePath)!);
        RawDatabase.Execute(_directory.StatePath, ddl);
        RawDatabase.Execute(_directory.StatePath, "INSERT INTO checkpoint(id,schema,instance,project,route,cursor,attempts) VALUES(1,1,'consumer-a','source-a','route-a',0,0)");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>An existing file without checkpoint row is refused.</summary>
    [Fact]
    public async Task AnExistingFileWithoutCheckpointRowIsRefused()
    {
        using (await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None))
        {
        }
        RawDatabase.Execute(_directory.StatePath, "DELETE FROM checkpoint");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>Inconsistent stored values are refused on read.</summary>
    [Theory]
    [InlineData("UPDATE checkpoint SET phase='delivered', pending='{}'")]
    [InlineData("UPDATE checkpoint SET phase='accepted', pending='{}'")]
    [InlineData("UPDATE checkpoint SET phase='pending', pending=NULL")]
    [InlineData("UPDATE checkpoint SET phase=NULL, pending='{}'")]
    [InlineData("UPDATE checkpoint SET phase='pending', pending=X'C3'")]
    [InlineData("UPDATE checkpoint SET phase='pending', pending=CAST(X'C328' AS TEXT)")]
    [InlineData("UPDATE checkpoint SET attempts=-1")]
    [InlineData("UPDATE checkpoint SET attempts=2147483648")]
    public async Task InconsistentStoredValuesAreRefusedOnRead(string corruption)
    {
        using (await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None))
        {
        }
        RawDatabase.Execute(_directory.StatePath, corruption);

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, async () =>
        {
            using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
            await checkpoint.ReadAsync(CancellationToken.None);
        });
    }

    /// <summary>A grant journal with another structure is refused at open.</summary>
    [Fact]
    public async Task AGrantJournalWithAnotherStructureIsRefusedAtOpen()
    {
        using (await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None))
        {
        }
        RawDatabase.Execute(_directory.StatePath, "CREATE TABLE delivery_replays(request_id TEXT PRIMARY KEY,pending_id TEXT NOT NULL,receipt TEXT NOT NULL)");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>Unknown schema objects are refused at open.</summary>
    [Fact]
    public async Task UnknownSchemaObjectsAreRefusedAtOpen()
    {
        using (await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None))
        {
        }
        RawDatabase.Execute(_directory.StatePath, "CREATE TRIGGER rewrite AFTER UPDATE ON checkpoint BEGIN UPDATE checkpoint SET attempts=0; END");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
