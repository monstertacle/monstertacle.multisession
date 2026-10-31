using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Archived and unknown dispatch evidence is validated without granting another send.</summary>
public sealed class DispatchStorageConsistencyTests
{
    private static readonly string[] _corruptions = ["request", "session", "sequence", "submission", "input", "model", "profile", "settings",
        "revision", "session-revision", "ack-session", "ack-operation", "json", "reservation-time", "workspace", "release", "missing", "dispatch-null", "binding-null", "frozen-null"];

    /// <summary>Every corruption is exercised for both occupied unknown and released confirmed dispatches.</summary>
    public static IEnumerable<object[]> Corruptions()
    {
        foreach (string corruption in _corruptions)
        {
            yield return [corruption, true];
            yield return [corruption, false];
        }
    }

    /// <summary>Immutable reservation evidence cannot disappear or drift while public request/result snapshots remain valid.</summary>
    [Theory]
    [MemberData(nameof(Corruptions))]
    public async Task RejectsDispatchGraphDrift(string corruption, bool unknown)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        if (unknown) { await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None); }
        else { await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None); }
        await fixture.Host.Store.DisposeAsync();
        DispatchStoreTampering.Apply(fixture.Host.Directory.DatabasePath, corruption);
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }
}
