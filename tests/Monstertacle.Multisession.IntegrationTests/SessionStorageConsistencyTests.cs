using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Frozen session identity and lifecycle cannot drift from the indexed SQL projection.</summary>
public sealed class SessionStorageConsistencyTests
{
    /// <summary>All session snapshots are validated even when no request or opening recovery reads them.</summary>
    [Theory]
    [InlineData("SessionId")]
    [InlineData("ProviderId")]
    [InlineData("AgentProfileId")]
    [InlineData("SettingsRevision")]
    [InlineData("BindingRevision")]
    [InlineData("Lifecycle")]
    [InlineData("BlockReason")]
    [InlineData("CreatedUtc")]
    [InlineData("UpdatedUtc")]
    [InlineData("ClosedUtc")]
    [InlineData("Context")]
    [InlineData("context-owner")]
    [InlineData("model")]
    [InlineData("execution-kind")]
    [InlineData("adapter")]
    [InlineData("version")]
    [InlineData("Binding")]
    [InlineData("malformed")]
    [InlineData("missing")]
    public async Task RejectsSnapshotDriftBeforeAnyRecoveryMutation(string corruption)
    {
        await using AdmissionStoreFixture fixture = await SessionAsync();
        JsonNode snapshot = Snapshot(fixture);
        switch (corruption)
        {
            case "SessionId": snapshot[corruption] = Guid.NewGuid(); break;
            case "BindingRevision": snapshot[corruption] = 42; break;
            case "Lifecycle": snapshot[corruption] = 123456; break;
            case "BlockReason": snapshot[corruption] = 123456; break;
            case "CreatedUtc": case "UpdatedUtc": case "ClosedUtc": snapshot[corruption] = DateTimeOffset.UtcNow; break;
            case "Context": case "Binding": snapshot[corruption] = null; break;
            case "context-owner": snapshot["Context"]!["SessionId"] = Guid.NewGuid(); break;
            case "model": snapshot["Context"]!["ModelId"] = "changed-model"; break;
            case "execution-kind": snapshot["Context"]!["ExecutionKind"] = 123456; break;
            case "adapter": snapshot["Binding"]!["AdapterId"] = "changed-adapter"; break;
            case "version": snapshot["Binding"]!["FormatVersion"] = 42; break;
            case "malformed": case "missing": break;
            default: snapshot[corruption] = "changed-identifier"; break;
        }
        string json = corruption == "malformed" ? "{" : corruption == "missing" ? "{}" : snapshot.ToJsonString();
        StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Sessions SET SnapshotJson=$json", ("$json", json));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture);
    }

    /// <summary>Lifecycle and allocation invariants hold even when JSON and SQL agree on an invalid state.</summary>
    [Theory]
    [InlineData("next-sequence")]
    [InlineData("open-without-binding")]
    [InlineData("closed-without-time")]
    public async Task RejectsInvalidAgreedProjection(string corruption)
    {
        await using AdmissionStoreFixture fixture = await SessionAsync();
        JsonNode snapshot = Snapshot(fixture);
        if (corruption == "next-sequence") { StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Sessions SET NextSequence=42"); }
        if (corruption == "open-without-binding")
        {
            snapshot["Binding"] = null;
            StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Sessions SET AdapterId=NULL,ContractVersion=NULL");
        }
        if (corruption == "closed-without-time")
        {
            snapshot["Lifecycle"] = (int)SessionLifecycle.Closed;
            StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Sessions SET Lifecycle=$closed", ("$closed", (int)SessionLifecycle.Closed));
        }
        StoreProbe.Execute(fixture.Directory.DatabasePath, "UPDATE Sessions SET SnapshotJson=$json", ("$json", snapshot.ToJsonString()));
        await RequestStorageConsistencyTests.AssertRejectedWithoutMutationAsync(fixture);
    }

    private static JsonNode Snapshot(AdmissionStoreFixture fixture)
        => JsonNode.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions")!)!;

    private static async Task<AdmissionStoreFixture> SessionAsync()
    {
        AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        try { await fixture.CreateSessionAsync(); await fixture.Store.DisposeAsync(); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }
}
