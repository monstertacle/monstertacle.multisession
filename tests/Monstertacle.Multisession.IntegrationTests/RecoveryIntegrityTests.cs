using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>A corrupt case cannot redirect an explicit operator decision to another attempt.</summary>
public sealed class RecoveryIntegrityTests
{
    /// <summary>Returned native case evidence must match its authoritative request and dispatch links.</summary>
    [Theory]
    [InlineData("RequestId")]
    [InlineData("DispatchId")]
    public async Task RejectsInconsistentCaseLinks(string property)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM RecoveryCases")!)!;
        snapshot[property] = Guid.NewGuid();
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "UPDATE RecoveryCases SET SnapshotJson=$json", ("$json", snapshot.ToJsonString()));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Host.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
    }
}
