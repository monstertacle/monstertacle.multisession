using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Frozen queued inputs retain their original content and valid execution constraints.</summary>
public sealed class SubmissionStorageConsistencyTests
{
    /// <summary>Malformed frozen submissions are rejected before recovery or reservation.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("null-input")]
    [InlineData("empty-input")]
    [InlineData("zero-timeout")]
    [InlineData("negative-timeout")]
    [InlineData("overflow-timeout")]
    [InlineData("workspace")]
    [InlineData("json")]
    public async Task RejectsInvalidFrozenSubmission(string corruption)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "original input", TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
        await fixture.Store.DisposeAsync();
        JsonNode submission = JsonNode.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SubmissionJson FROM Requests")!)!;
        switch (corruption)
        {
            case "owner": submission["SessionId"] = Guid.NewGuid(); break;
            case "null-input": submission["Input"] = null; break;
            case "empty-input": submission["Input"] = string.Empty; break;
            case "zero-timeout": submission["Timeout"] = "00:00:00"; break;
            case "negative-timeout": submission["Timeout"] = "-00:00:01"; break;
            case "overflow-timeout": submission["Timeout"] = TimeSpan.MaxValue.ToString("c", System.Globalization.CultureInfo.InvariantCulture); break;
            case "workspace": submission["WriteIntent"] = true; submission["WorkspaceKey"] = "foreign-workspace"; break;
        }
        StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Requests SET SubmissionJson=$json", ("$json", corruption == "json" ? "{" : submission.ToJsonString()));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture);
    }

    /// <summary>Reopening with a lower input limit does not retroactively reject an already accepted complete input.</summary>
    [Fact]
    public async Task RetainsAcceptedInputWhenNewHostLimitIsSmaller()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync(maxInputBytes: 4096);
        SessionSnapshot session = await fixture.CreateSessionAsync();
        string input = new('x', 2000);
        await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, input, TimeSpan.FromSeconds(5), null, false)), 100, CancellationToken.None);
        await fixture.Store.DisposeAsync();
        await using ISessionStore reopened = await SqliteStoreFactory.OpenExistingAsync(fixture.Directory, fixture.Directory.TrustScopeId,
            fixture.Options with { MaxInputBytes = 1 }, CancellationToken.None);
        await reopened.StartAsync(fixture.Directory, CancellationToken.None);
        ExecutionReservation reservation = (await reopened.TryReserveHeadAsync(4, CancellationToken.None))!;
        ReservedDispatch dispatch = (await reopened.GetReservedDispatchAsync(reservation.DispatchId, CancellationToken.None))!;
        Assert.Equal(input, dispatch.Submission.Input);
    }
}
