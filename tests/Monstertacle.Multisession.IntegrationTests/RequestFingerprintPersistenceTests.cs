using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Durable immutable input comparison after receipt loss and before startup recovery.</summary>
public sealed class RequestFingerprintPersistenceTests
{
    /// <summary>The original caller identity recovers the accepted input identity after reopening.</summary>
    [Fact]
    public async Task ReopenRetainsAcceptedSubmissionFingerprint()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        SubmitRequest input = new(session.SessionId, "original input", TimeSpan.FromSeconds(5), null, false);
        Guid requestId = Guid.NewGuid();
        await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(requestId, input, default);
        await fixture.ReopenAsync();
        RequestSnapshot stored = (await fixture.Store.GetRequestAsync(requestId, default))!;
        Assert.NotEmpty(stored.SubmissionFingerprint);
        Assert.Equal(RequestSubmissionFingerprint.Compute(input), stored.SubmissionFingerprint);
        Assert.Equal(requestId, stored.RequestId);
    }

    /// <summary>Another valid input or substituted digest cannot survive pre-recovery consistency checks.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsChangedValidPayloadOrFingerprintWithoutMutation(bool changeFingerprint)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "original input", TimeSpan.FromSeconds(5), null, false)), 100, default);
        await fixture.Store.DisposeAsync();
        string column = changeFingerprint ? "SnapshotJson" : "SubmissionJson";
        JsonNode value = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, $"SELECT {column} FROM Requests")!)!;
        value[changeFingerprint ? "SubmissionFingerprint" : "Input"] = "replaced valid input";
        StoreProbe.Execute(fixture.Directory.DatabasePath, $"UPDATE Requests SET {column}=$json", ("$json", value.ToJsonString()));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture);
    }
}
