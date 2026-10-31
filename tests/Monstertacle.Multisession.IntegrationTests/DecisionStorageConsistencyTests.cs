using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Stored operator decisions remain attributable to the exact case revision they resolved.</summary>
public sealed class DecisionStorageConsistencyTests
{
    /// <summary>Resolved state cannot survive without a coherent immutable confirmation record.</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("target-kind")]
    [InlineData("target-id")]
    [InlineData("zero-revision")]
    [InlineData("future-revision")]
    [InlineData("kind")]
    [InlineData("explanation")]
    [InlineData("evidence")]
    [InlineData("json")]
    [InlineData("applied")]
    [InlineData("missing")]
    [InlineData("latest-close")]
    public async Task RejectsDecisionGraphDrift(string corruption)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await fixture.Host.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await fixture.Host.Store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "external end confirmed", "non-secret evidence"), fixture.Session.Binding, CancellationToken.None);
        await fixture.Host.Store.DisposeAsync();
        Corrupt(fixture.Host.Directory.DatabasePath, corruption);
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture.Host);
    }

    private static void Corrupt(string path, string corruption)
    {
        if (corruption == "missing") { StoreProbe.Execute(path, "DELETE FROM RecoveryDecisions"); return; }
        if (corruption == "json") { StoreProbe.Execute(path, "UPDATE RecoveryDecisions SET DecisionJson='{' "); return; }
        if (corruption == "applied") { StoreProbe.Execute(path, "UPDATE RecoveryDecisions SET AppliedUtc='2026-01-01T01:00:00+01:00'"); return; }
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(path, "SELECT DecisionJson FROM RecoveryDecisions")!)!;
        switch (corruption)
        {
            case "identity": snapshot["DecisionId"] = Guid.NewGuid(); break;
            case "target-kind": snapshot["Target"]!["Kind"] = 123456; break;
            case "target-id": snapshot["Target"]!["Id"] = Guid.NewGuid(); break;
            case "zero-revision": snapshot["ExpectedRevision"] = 0; break;
            case "future-revision": snapshot["ExpectedRevision"] = 42; break;
            case "kind": snapshot["Kind"] = 123456; break;
            case "explanation": snapshot["Explanation"] = string.Empty; break;
            case "evidence": snapshot["EvidenceReference"] = string.Empty; break;
            case "latest-close": snapshot["Kind"] = (int)RecoveryDecisionKind.CloseSession; break;
            default: throw new ArgumentException("Unknown controlled corruption.", nameof(corruption));
        }
        StoreProbe.Execute(path, "UPDATE RecoveryDecisions SET DecisionJson=$json", ("$json", snapshot.ToJsonString()));
    }
}
