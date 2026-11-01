using Xunit;

namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>Further current-format guards: encoding, required table, column storage classes and position bounds.</summary>
public sealed class FormatGuardTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>A database in another text encoding is refused.</summary>
    [Fact]
    public async Task AnotherTextEncodingIsRefused()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory.StatePath)!);
        RawDatabase.Execute(_directory.StatePath, "PRAGMA encoding='UTF-16le'; CREATE TABLE " + CheckpointFormat.CheckpointTable);
        RawDatabase.Execute(_directory.StatePath, "INSERT INTO checkpoint(id,schema,instance,project,route,cursor) VALUES(1,1,'consumer-a','source-a','route-a',0)");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>An existing database without the checkpoint table is refused by the inspection open.</summary>
    [Fact]
    public async Task AnExistingDatabaseWithoutTheCheckpointTableIsRefused()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory.StatePath)!);
        RawDatabase.Execute(_directory.StatePath, "PRAGMA user_version=0");

        await Refusals.Expect(DeliveryRefusal.CheckpointCorrupt, () => DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
    }

    /// <summary>Stored values of another storage class are refused on read.</summary>
    [Theory]
    [InlineData("UPDATE checkpoint SET cursor='x'")]
    [InlineData("UPDATE checkpoint SET attempts='x'")]
    [InlineData("UPDATE checkpoint SET phase=1")]
    [InlineData("UPDATE checkpoint SET receipt=X'00'")]
    [InlineData("UPDATE checkpoint SET cursor=-1")]
    public async Task StoredValuesOfAnotherStorageClassAreRefused(string corruption)
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

    /// <summary>Positions cannot wrap around.</summary>
    [Fact]
    public async Task PositionsCannotWrapAround()
    {
        var maximum = new DeliveryCheckpointOptions(_directory.StatePath, Payloads.Channel) { InitialPosition = long.MaxValue };
        await Assert.ThrowsAsync<ArgumentException>(() => DeliveryCheckpoint.OpenAsync(maximum, CancellationToken.None));
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => checkpoint.StageAsync(0, Payloads.Item(0), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => checkpoint.StageAsync(long.MinValue, Payloads.Item(1), CancellationToken.None));
        Assert.Null((await checkpoint.ReadAsync(CancellationToken.None)).Pending);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
