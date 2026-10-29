using System.Text;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiResponse
{
    internal static string Identity(JsonElement response)
    {
        string? id = response.GetProperty("id").GetString();
        if (response.GetProperty("object").GetString() != "response" || string.IsNullOrWhiteSpace(id))
        {
            throw OpenAiFailure.Create(ErrorCode.ProviderUnavailable);
        }
        return id;
    }
    internal static bool IsPending(JsonElement response) => response.GetProperty("status").GetString() is "queued" or "in_progress";

    internal static ProviderReply Reply(JsonElement response, ProviderBinding? binding, int maximumOutputBytes)
    {
        if (binding?.SessionReference is { } original && response.TryGetProperty("conversation", out JsonElement conversation)
            && conversation.ValueKind != JsonValueKind.Null)
        {
            string? reference = conversation.ValueKind == JsonValueKind.String ? conversation.GetString() : conversation.GetProperty("id").GetString();
            if (reference != original) { return Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
        }
        return response.GetProperty("status").GetString() switch
        {
            "queued" or "in_progress" => new(RequestState.Running, ExecutionCertainty.InProgress, string.Empty, false, null, null),
            "completed" => Completed(response, binding, maximumOutputBytes),
            "failed" => Failure(NativeError(response), ExecutionCertainty.ConfirmedEnded),
            "incomplete" => Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded),
            "cancelled" => new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, new PublicError(ErrorCode.Cancelled, ExecutionCertainty.ConfirmedEnded)),
            "requires_action" => Failure(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown),
            _ => Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown),
        };
    }

    private static ProviderReply Completed(JsonElement response, ProviderBinding? binding, int maximumOutputBytes)
    {
        StringBuilder output = new();
        int bytes = 0;
        foreach (JsonElement item in response.GetProperty("output").EnumerateArray())
        {
            string? type = item.GetProperty("type").GetString();
            if (type == "reasoning") { continue; }
            if (type != "message") { return Failure(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.ConfirmedEnded); }
            foreach (JsonElement content in item.GetProperty("content").EnumerateArray())
            {
                if (content.GetProperty("type").GetString() != "output_text") { continue; }
                string? text = content.GetProperty("text").GetString();
                int partBytes = Encoding.UTF8.GetByteCount(text ?? string.Empty);
                if (partBytes > maximumOutputBytes - bytes) { return Failure(ErrorCode.OutputLimitExceeded, ExecutionCertainty.ConfirmedEnded); }
                bytes += partBytes;
                output.Append(text);
            }
        }
        return output.Length == 0 ? Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded)
            : new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output.ToString(), true, binding, null);
    }
    internal static ErrorCode NativeError(JsonElement response)
        => response.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.String
            && code.GetString() == "context_length_exceeded" ? ErrorCode.ProviderContextLimit : ErrorCode.ExecutionFailed;
    internal static ProviderReply Failure(ErrorCode code, ExecutionCertainty certainty)
        => new(RequestState.Failed, certainty, string.Empty, false, null, new PublicError(code, certainty));
}
