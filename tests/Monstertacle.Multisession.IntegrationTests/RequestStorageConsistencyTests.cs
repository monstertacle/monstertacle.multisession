using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Valid schema alone cannot authorize recovery writes over inconsistent request data.</summary>
public sealed class RequestStorageConsistencyTests
{
    /// <summary>Illegal persisted state/evidence pairs are rejected before recovery and leave the file unchanged.</summary>
    [Theory]
    [InlineData(RequestState.Queued, ExecutionCertainty.Unknown)]
    [InlineData(RequestState.Succeeded, ExecutionCertainty.Unknown)]
    [InlineData(RequestState.Failed, ExecutionCertainty.InProgress)]
    [InlineData(RequestState.Cancelled, ExecutionCertainty.InProgress)]
    [InlineData(RequestState.TimedOut, ExecutionCertainty.InProgress)]
    [InlineData(RequestState.Interrupted, ExecutionCertainty.NotStarted)]
    [InlineData((RequestState)123456, ExecutionCertainty.NotStarted)]
    [InlineData(RequestState.Queued, (ExecutionCertainty)123456)]
    public async Task RejectsIllegalPersistedPair(RequestState state, ExecutionCertainty certainty)
    {
        await using AdmissionStoreFixture fixture = await QueuedAsync();
        JsonNode snapshot = ReadSnapshot(fixture);
        snapshot["State"] = (int)state;
        snapshot["Certainty"] = (int)certainty;
        StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Requests SET State=$state,Certainty=$certainty,SnapshotJson=$json",
            ("$state", (int)state), ("$certainty", (int)certainty), ("$json", snapshot.ToJsonString()));
        await AssertRejectedWithoutMutationAsync(fixture);
    }

    /// <summary>Malformed or contradictory snapshots are not ignored merely because the row is queued.</summary>
    [Theory]
    [InlineData("malformed")]
    [InlineData("missing")]
    [InlineData("RequestId")]
    [InlineData("SessionId")]
    [InlineData("Sequence")]
    [InlineData("State")]
    [InlineData("Certainty")]
    [InlineData("AcceptedUtc")]
    [InlineData("StartedUtc")]
    [InlineData("EndedUtc")]
    [InlineData("CancellationRequestedUtc")]
    [InlineData("Result")]
    public async Task RejectsSnapshotDrift(string property)
    {
        await using AdmissionStoreFixture fixture = await QueuedAsync();
        JsonNode snapshot = ReadSnapshot(fixture);
        switch (property)
        {
            case "RequestId": case "SessionId": snapshot[property] = Guid.NewGuid(); break;
            case "Sequence": snapshot[property] = 42; break;
            case "State": snapshot[property] = (int)RequestState.Running; break;
            case "Certainty": snapshot[property] = (int)ExecutionCertainty.Unknown; break;
            case "Result": snapshot[property] = new JsonObject { ["Outcome"] = (int)RequestState.Succeeded, ["Certainty"] = (int)ExecutionCertainty.ConfirmedEnded }; break;
            case "malformed": case "missing": break;
            default: snapshot[property] = DateTimeOffset.UtcNow; break;
        }
        string json = property == "malformed" ? "{" : property == "missing" ? "{}" : snapshot.ToJsonString();
        StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Requests SET SnapshotJson=$json", ("$json", json));
        await AssertRejectedWithoutMutationAsync(fixture);
    }

    private static JsonNode ReadSnapshot(AdmissionStoreFixture fixture)
        => JsonNode.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Requests")!)!;

    private static async Task<AdmissionStoreFixture> QueuedAsync()
    {
        AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        try
        {
            SessionSnapshot session = await fixture.CreateSessionAsync();
            await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "retained input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
            await fixture.Store.DisposeAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    internal static async Task AssertRejectedWithoutMutationAsync(AdmissionStoreFixture fixture)
    {
        byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(fixture.Directory.DatabasePath));
        await using (ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(fixture.Directory, fixture.Directory.TrustScopeId, fixture.Options, CancellationToken.None))
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => store.StartAsync(fixture.Directory, CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.DoesNotContain("retained input", failure.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(fixture.Directory.DatabasePath)));
    }
}
