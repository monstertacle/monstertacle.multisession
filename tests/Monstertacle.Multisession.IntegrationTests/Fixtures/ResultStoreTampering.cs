using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Direct mutations are confined to one isolated test store and never exposed by production code.
internal static class ResultStoreTampering
{
    internal static void Apply(string path, string corruption)
    {
        JsonNode result = Read(path, "SELECT ReplyJson FROM Results");
        JsonNode history = Read(path, "SELECT SnapshotJson FROM History");
        JsonNode submission = Read(path, "SELECT SubmissionJson FROM Requests");
        switch (corruption)
        {
            case "result-missing": StoreProbe.Execute(path, "DELETE FROM Results"); return;
            case "history-missing": StoreProbe.Execute(path, "DELETE FROM History"); return;
            case "completed-time": StoreProbe.Execute(path, "UPDATE Results SET CompletedUtc='2000-01-01T00:00:00.0000000+00:00'"); return;
            case "result-json": StoreProbe.Execute(path, "UPDATE Results SET ReplyJson='{' "); return;
            case "history-json": StoreProbe.Execute(path, "UPDATE History SET SnapshotJson='{' "); return;
            case "submission-json": StoreProbe.Execute(path, "UPDATE Requests SET SubmissionJson='{' "); return;
            case "history-duplicate": DuplicateHistory(path, history); return;
            case "result-output": result["Output"] = "replaced output"; break;
            case "result-certainty": result["Certainty"] = (int)ExecutionCertainty.Unknown; break;
            case "history-sequence": history["Sequence"] = 42; StoreProbe.Execute(path, "UPDATE History SET Sequence=42"); break;
            case "history-ordinal": history["HistoryOrdinal"] = 42; StoreProbe.Execute(path, "UPDATE History SET HistoryOrdinal=42"); break;
            case "history-input": history["Input"] = "replaced input"; break;
            case "history-result": history["Result"]!["Output"] = "replaced output"; break;
            case "history-request": history["RequestId"] = Guid.NewGuid(); break;
            case "history-owner": history["SessionId"] = Guid.NewGuid(); break;
            case "history-id": history["HistoryId"] = Guid.NewGuid(); break;
            case "submission-owner": submission["SessionId"] = Guid.NewGuid(); break;
            case "submission-input": submission["Input"] = "replaced input"; break;
            default: throw new ArgumentException("Unknown controlled corruption.", nameof(corruption));
        }
        if (corruption.StartsWith("result-", StringComparison.Ordinal)) { StoreProbe.Execute(path, "UPDATE Results SET ReplyJson=$json", ("$json", result.ToJsonString())); }
        else if (corruption.StartsWith("submission-", StringComparison.Ordinal)) { StoreProbe.Execute(path, "UPDATE Requests SET SubmissionJson=$json", ("$json", submission.ToJsonString())); }
        else { StoreProbe.Execute(path, "UPDATE History SET SnapshotJson=$json", ("$json", history.ToJsonString())); }
    }

    private static JsonNode Read(string path, string sql) => JsonNode.Parse((string)StoreProbe.Scalar(path, sql)!)!;

    private static void DuplicateHistory(string path, JsonNode history)
    {
        Guid identity = Guid.NewGuid();
        history["HistoryId"] = identity;
        history["HistoryOrdinal"] = 2;
        StoreProbe.Execute(path, """
            INSERT INTO History (HistoryId,SessionId,RequestId,Sequence,HistoryOrdinal,SnapshotJson)
            SELECT $id,SessionId,RequestId,Sequence,2,$json FROM History
            """, ("$id", identity.ToString("D")), ("$json", history.ToJsonString()));
    }
}
