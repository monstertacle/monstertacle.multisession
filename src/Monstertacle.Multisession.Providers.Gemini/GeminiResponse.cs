using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Providers.Gemini;

internal static class GeminiResponse
{
    internal static string Identity(JsonElement response)
    {
        string? id = response.GetProperty("id").GetString();
        if (response.GetProperty("object").GetString() != "interaction" || string.IsNullOrWhiteSpace(id))
        {
            throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable);
        }
        return id;
    }
    internal static bool IsPending(JsonElement response) => response.GetProperty("status").GetString() == "in_progress";
    internal static bool MatchesLineage(JsonElement response, ProviderBinding binding)
    {
        if (!response.TryGetProperty("previous_interaction_id", out JsonElement previous)) { return true; }
        return previous.ValueKind == JsonValueKind.Null ? binding.ContinuationState is null
            : previous.ValueKind == JsonValueKind.String && previous.GetString() == binding.ContinuationState;
    }
    internal static ProviderReply Reply(JsonElement response, ProviderBinding? binding, int maximumOutputBytes)
    {
        if (binding is not null && !MatchesLineage(response, binding)) { return Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
        ProviderBinding? known = binding is null ? null : binding with { SessionReference = binding.SessionReference ?? Identity(response) };
        return response.GetProperty("status").GetString() switch
        {
            "in_progress" => new(RequestState.Running, ExecutionCertainty.InProgress, string.Empty, false, null, null),
            "completed" => Completed(response, known, maximumOutputBytes),
            "failed" or "incomplete" => Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded, known),
            "cancelled" => Failure(ErrorCode.Cancelled, ExecutionCertainty.ConfirmedEnded, known) with { Outcome = RequestState.Cancelled },
            "requires_action" or "requires_continuation" => Failure(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown),
            _ => Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown),
        };
    }
    private static ProviderReply Completed(JsonElement response, ProviderBinding? binding, int maximumOutputBytes)
    {
        string output = string.Concat(response.GetProperty("steps").EnumerateArray()
            .Where(step => step.GetProperty("type").GetString() == "model_output")
            .SelectMany(step => step.GetProperty("content").EnumerateArray())
            .Where(content => content.GetProperty("type").GetString() == "text")
            .Select(content => content.GetProperty("text").GetString()));
        ProviderBinding? confirmed = binding is null ? null : binding with { ContinuationState = Identity(response) };
        return ProviderReplyPolicy.NormalizeOutput(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output, true, confirmed, null), maximumOutputBytes);
    }
    internal static ProviderReply Failure(ErrorCode code, ExecutionCertainty certainty, ProviderBinding? binding = null)
        => new(RequestState.Failed, certainty, string.Empty, false, binding, new PublicError(code, certainty));
}
