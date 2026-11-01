using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Attempt grants change no row, so the in-transaction expectation check is their only guard.</summary>
public sealed class StaleAttemptGrantTests : IDisposable
{
    private const string _context = "context-digest";
    private readonly TestDirectory _directory = new();

    /// <summary>A stale expectation records no attempt grant.</summary>
    [Theory]
    [InlineData("route")]
    [InlineData("position")]
    [InlineData("digest")]
    [InlineData("attempts")]
    public async Task AStaleExpectationRecordsNoAttemptGrant(string field)
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        var stale = field switch
        {
            "route" => expectation with { RouteId = "route-b" },
            "position" => expectation with { Position = 1 },
            "digest" => expectation with { PendingDigest = new string('0', 64) },
            _ => expectation with { Attempts = 1 },
        };

        await Refusals.Expect(DeliveryRefusal.StaleExpectation, () => new AttemptGrants(checkpoint)
            .RecordAsync(new AttemptGrant(Guid.NewGuid(), stale, Guid.NewGuid(), _context, "g"), CancellationToken.None));

        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "preparation_rearms"));
    }

    /// <summary>A repeated attempt-grant request id with any other stored value conflicts.</summary>
    [Theory]
    [InlineData("context")]
    [InlineData("route")]
    [InlineData("position")]
    [InlineData("digest")]
    [InlineData("item")]
    public async Task ARepeatedAttemptGrantWithAnotherStoredValueConflicts(string field)
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        var grant = new AttemptGrant(Guid.NewGuid(), expectation, Guid.NewGuid(), _context, "g");
        var grants = new AttemptGrants(checkpoint);
        await grants.RecordAsync(grant, CancellationToken.None);
        var other = field switch
        {
            "context" => grant with { ContextDigest = "other" },
            "route" => grant with { Expected = expectation with { RouteId = "route-b" } },
            "position" => grant with { Expected = expectation with { Position = 3 } },
            "digest" => grant with { Expected = expectation with { PendingDigest = new string('1', 64) } },
            _ => grant with { ItemId = Guid.NewGuid() },
        };

        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => grants.RecordAsync(other, CancellationToken.None));
    }

    /// <summary>A repeated route-move request id with another item or old route conflicts.</summary>
    [Theory]
    [InlineData("item")]
    [InlineData("route")]
    public async Task ARepeatedRouteMoveWithAnotherItemOrOldRouteConflicts(string field)
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        var move = new RouteMove(Guid.NewGuid(), expectation, Guid.NewGuid(), "route-b", "m");
        var moves = new RouteMoves(checkpoint);
        await moves.MoveAsync(move, CancellationToken.None);
        var other = field == "item" ? move with { ItemId = Guid.NewGuid() } : move with { Expected = expectation with { RouteId = "route-z" } };

        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => moves.MoveAsync(other, CancellationToken.None));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
