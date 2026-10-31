using System.Globalization;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Stored terminal results and dispatch timestamps remain consistent with public snapshots.</summary>
public sealed class TerminalRequestStorageConsistencyTests
{
    /// <summary>Contradictory stored finalization metadata cannot be repaired implicitly by startup.</summary>
    [Theory]
    [InlineData("missing-result")]
    [InlineData("outcome")]
    [InlineData("certainty")]
    [InlineData("missing-ended")]
    [InlineData("non-utc")]
    public async Task RejectsTerminalProjectionDrift(string corruption)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM Requests")!)!;
        switch (corruption)
        {
            case "missing-result": snapshot["Result"] = null; break;
            case "outcome": snapshot["Result"]!["Outcome"] = (int)RequestState.Failed; break;
            case "certainty": snapshot["Result"]!["Certainty"] = (int)ExecutionCertainty.Unknown; break;
            case "missing-ended": snapshot["EndedUtc"] = null; StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE Requests SET EndedUtc=NULL"); break;
            case "non-utc":
                DateTimeOffset accepted = DateTimeOffset.Parse(snapshot["AcceptedUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture);
                snapshot["AcceptedUtc"] = accepted.ToOffset(TimeSpan.FromHours(2));
                break;
        }
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE Requests SET SnapshotJson=$json", ("$json", snapshot.ToJsonString()));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }

    /// <summary>A Running row cannot silently lose its actual dispatch timestamp during recovery.</summary>
    [Fact]
    public async Task RejectsRunningWithoutStartTimestamp()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.DisposeAsync();
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM Requests")!)!;
        snapshot["StartedUtc"] = null;
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE Requests SET StartedUtc=NULL,SnapshotJson=$json", ("$json", snapshot.ToJsonString()));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }
}
