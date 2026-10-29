using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Inconsistent persisted identities cannot grant permission to execute a different request.</summary>
public sealed class ReservedSnapshotIntegrityTests
{
    /// <summary>The snapshot graph must agree with the authoritative reserved row.</summary>
    [Theory]
    [InlineData("session")]
    [InlineData("submission")]
    [InlineData("request")]
    public async Task RejectsInconsistentReservedSnapshot(string corrupted)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        JsonNode envelope = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests")!)!;
        JsonNode snapshot = envelope["Snapshot"]!;
        if (corrupted == "session") { snapshot["Session"]!["SessionId"] = Guid.NewGuid(); }
        if (corrupted == "submission") { snapshot["Submission"]!["SessionId"] = Guid.NewGuid(); }
        if (corrupted == "request") { snapshot["Reservation"]!["RequestId"] = Guid.NewGuid(); }
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE Requests SET ReservedDispatchJson=$json", ("$json", envelope.ToJsonString()));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Host.Store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
    }
}
