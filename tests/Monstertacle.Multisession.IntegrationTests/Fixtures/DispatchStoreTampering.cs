using System.Text.Json.Nodes;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal static class DispatchStoreTampering
{
    internal static void Apply(string path, string corruption)
    {
        if (MutateColumns(path, corruption)) { return; }
        if (corruption == "json") { StoreProbe.Execute(path, "UPDATE Requests SET ReservedDispatchJson='{' "); return; }
        JsonNode envelope = JsonNode.Parse((string)StoreProbe.Scalar(path, "SELECT ReservedDispatchJson FROM Requests")!)!;
        JsonNode snapshot = envelope["Snapshot"]!;
        switch (corruption)
        {
            case "request": snapshot["Reservation"]!["RequestId"] = Guid.NewGuid(); break;
            case "session": snapshot["Session"]!["SessionId"] = Guid.NewGuid(); break;
            case "sequence": snapshot["Sequence"] = 42; break;
            case "submission": snapshot["Submission"]!["SessionId"] = Guid.NewGuid(); break;
            case "input": snapshot["Submission"]!["Input"] = "replaced input"; break;
            case "model": snapshot["Session"]!["Context"]!["ModelId"] = "replaced model"; break;
            case "profile": snapshot["Session"]!["AgentProfileId"] = "replaced profile"; break;
            case "settings": snapshot["Session"]!["Context"]!["Settings"]!["I"] = "replaced setting"; break;
            case "revision": snapshot["BindingRevision"] = 42; break;
            case "session-revision": snapshot["Session"]!["BindingRevision"] = 42; break;
            case "ack-session": envelope["SessionReference"] = "foreign-native-session"; break;
            case "ack-operation": envelope["OperationReference"] = string.Empty; break;
            default: throw new ArgumentException("Unknown controlled corruption.", nameof(corruption));
        }
        StoreProbe.Execute(path, "UPDATE Requests SET ReservedDispatchJson=$json", ("$json", envelope.ToJsonString()));
    }

    private static bool MutateColumns(string path, string corruption)
    {
        string? sql = corruption switch
        {
            "reservation-time" => "UPDATE Reservations SET ReservedUtc='2000-01-01T00:00:00.0000000+00:00'",
            "workspace" => "UPDATE Reservations SET WorkspaceKey='foreign-workspace'",
            "release" => "UPDATE Reservations SET ReleasedUtc='2000-01-01T00:00:00.0000000+00:00',ReleaseReason=NULL",
            "missing" => "DELETE FROM RecoveryCases; DELETE FROM Reservations",
            "dispatch-null" => "UPDATE Requests SET DispatchId=NULL,BindingRevision=NULL,ReservedDispatchJson=NULL",
            "binding-null" => "UPDATE Requests SET BindingRevision=NULL",
            "frozen-null" => "UPDATE Requests SET ReservedDispatchJson=NULL",
            _ => null,
        };
        if (sql is null) { return false; }
        StoreProbe.Execute(path, sql);
        return true;
    }
}
