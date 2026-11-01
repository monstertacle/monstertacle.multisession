using Microsoft.Data.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>A failing state change after the receipt insert rolls the receipt back with it (real SQLite transaction).</summary>
public sealed class AtomicityTests : IDisposable
{
    private const string _failCheckpointUpdates = "CREATE TRIGGER refuse_update BEFORE UPDATE ON checkpoint BEGIN SELECT RAISE(ABORT, 'injected'); END";
    private readonly TestDirectory _directory = new();

    /// <summary>A failed resend transition leaves no receipt.</summary>
    [Fact]
    public async Task AFailedResendTransitionLeavesNoReceipt()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.UncertainAsync(checkpoint);
        var item = Guid.NewGuid();
        await new ResendAuthorizations(checkpoint).FindAsync(Guid.NewGuid(), CancellationToken.None);
        RawDatabase.Execute(_directory.StatePath, _failCheckpointUpdates);

        await Assert.ThrowsAsync<SqliteException>(() => new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, item, "r"), CancellationToken.None));

        Assert.True(RawDatabase.Count(_directory.StatePath, "delivery_replays") <= 0);
        RawDatabase.Execute(_directory.StatePath, "DROP TRIGGER refuse_update");
        Assert.True((await new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, item, "r"), CancellationToken.None)).Recorded);
    }

    /// <summary>A failed route change leaves no receipt.</summary>
    [Fact]
    public async Task AFailedRouteChangeLeavesNoReceipt()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        RawDatabase.Execute(_directory.StatePath, _failCheckpointUpdates);

        await Assert.ThrowsAsync<SqliteException>(() => new RouteMoves(checkpoint).MoveAsync(new RouteMove(Guid.NewGuid(), expectation, Guid.NewGuid(), "route-b", "m"), CancellationToken.None));

        Assert.True(RawDatabase.Count(_directory.StatePath, "delivery_rebinds") <= 0);
        Assert.Equal("route-a", RawDatabase.Scalar(_directory.StatePath, "SELECT route FROM checkpoint"));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
