using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal static class CaseStoreTampering
{
    internal static void Apply(string path, string corruption)
    {
        if (corruption == "missing") { StoreProbe.Execute(path, "DELETE FROM RecoveryCases"); return; }
        if (corruption == "json") { StoreProbe.Execute(path, "UPDATE RecoveryCases SET SnapshotJson='{' "); return; }
        JsonNode snapshot = JsonNode.Parse((string)StoreProbe.Scalar(path, "SELECT SnapshotJson FROM RecoveryCases")!)!;
        switch (corruption)
        {
            case "target-kind": snapshot["Target"]!["Kind"] = 123456; break;
            case "target-id": snapshot["Target"]!["Id"] = Guid.NewGuid(); break;
            case "SessionId": case "RequestId": case "DispatchId": snapshot[corruption] = Guid.NewGuid(); break;
            case "Revision": snapshot[corruption] = 42; break;
            case "Status": snapshot[corruption] = 123456; break;
            case "Certainty": snapshot[corruption] = (int)ExecutionCertainty.ConfirmedEnded; break;
            case "BlockReason": snapshot[corruption] = (int)SessionBlockReason.None; break;
            case "CreatedUtc": snapshot[corruption] = DateTimeOffset.UnixEpoch; break;
            case "Binding": snapshot[corruption] = null; break;
            case "native-session": snapshot["Binding"]!["SessionReference"] = "foreign-native-session"; break;
            case "native-head": snapshot["Binding"]!["ContinuationState"] = "foreign-native-head"; break;
            case "native-adapter": snapshot["Binding"]!["AdapterId"] = "foreign-adapter"; break;
            case "operation": snapshot["OperationReference"] = "foreign-operation"; break;
            case "resolved":
                snapshot["Status"] = (int)RecoveryCaseStatus.Resolved;
                snapshot["Certainty"] = (int)ExecutionCertainty.ConfirmedEnded;
                snapshot["BlockReason"] = (int)SessionBlockReason.None;
                StoreProbe.Execute(path, "UPDATE RecoveryCases SET Status=$status", ("$status", (int)RecoveryCaseStatus.Resolved));
                break;
            case "unblocked": ClearBlock(path); break;
            default: throw new ArgumentException("Unknown controlled corruption.", nameof(corruption));
        }
        StoreProbe.Execute(path, "UPDATE RecoveryCases SET SnapshotJson=$json", ("$json", snapshot.ToJsonString()));
    }

    private static void ClearBlock(string path)
    {
        JsonNode session = JsonNode.Parse((string)StoreProbe.Scalar(path, "SELECT SnapshotJson FROM Sessions")!)!;
        session["BlockReason"] = (int)SessionBlockReason.None;
        StoreProbe.Execute(path, "UPDATE Sessions SET BlockReason=$reason,SnapshotJson=$json", ("$reason", (int)SessionBlockReason.None), ("$json", session.ToJsonString()));
    }
}
