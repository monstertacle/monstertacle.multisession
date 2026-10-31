using System.Text.Json.Nodes;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

// Synthetic wire resources only; no transcript or execution implementation.
internal static class NativeWireReply
{
    internal static string Create(bool conversation, string id, string input, string? previous, bool pending, bool passiveContext)
    {
        JsonObject response = new()
        {
            ["id"] = id,
            ["object"] = conversation ? "response" : "interaction",
            ["status"] = pending ? conversation ? "queued" : "in_progress" : "completed",
        };
        if (conversation)
        {
            JsonArray output = [];
            if (!pending)
            {
                if (passiveContext) { output.Add(new JsonObject { ["type"] = "compaction", ["encrypted_content"] = "opaque-native-state" }); }
                output.Add(new JsonObject { ["type"] = "message", ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = input }) });
            }
            response["output"] = output;
        }
        else
        {
            response["steps"] = pending ? new JsonArray() : new JsonArray(new JsonObject
            {
                ["type"] = "model_output",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = input }),
            });
            if (previous is not null) { response["previous_interaction_id"] = previous; }
        }
        return response.ToJsonString();
    }
}
