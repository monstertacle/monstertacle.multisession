using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiProtocol
{
    internal const string AdapterId = "openai-responses";
    internal const int BindingVersion = 1;
    internal static bool IsAvailableModel(JsonElement metadata)
        => metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("object", out JsonElement kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "model"
            && metadata.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString());
    internal static ProviderBinding Conversation(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("object", out JsonElement kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() != "conversation"
            || !document.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable);
        }
        return new(AdapterId, BindingVersion, id.GetString(), null);
    }
    internal static string Input(ProviderCall request, ProviderBinding binding)
        => JsonSerializer.Serialize(new
        {
            model = request.Session.ModelId,
            conversation = binding.SessionReference,
            input = request.Input,
            background = true,
            store = true,
            instructions = request.Session.Settings.GetValueOrDefault("instructions"),
        });
}
