using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.Interactions;

internal static class InteractionProtocol
{
    internal const string AdapterId = "gemini-interactions";
    internal const int BindingVersion = 1;
    private const string _modelPrefix = "models/";
    internal static string ModelResourceId(string modelId)
        => modelId.StartsWith(_modelPrefix, StringComparison.Ordinal) ? modelId[_modelPrefix.Length..] : modelId;
    internal static bool IsAvailableModel(JsonElement metadata)
        => metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String
            && name.GetString() is { } identity && identity.Length > _modelPrefix.Length && identity.StartsWith(_modelPrefix, StringComparison.Ordinal);
    internal static string Input(ProviderCall request, ProviderBinding binding)
    {
        Dictionary<string, object?> body = new()
        {
            ["model"] = request.Session.ModelId,
            ["input"] = request.Input,
            ["background"] = true,
            ["store"] = true,
        };
        if (request.Session.Settings.TryGetValue("system_instruction", out string? instruction)) { body["system_instruction"] = instruction; }
        if (binding.ContinuationState is not null) { body["previous_interaction_id"] = binding.ContinuationState; }
        return JsonSerializer.Serialize(body);
    }
}
