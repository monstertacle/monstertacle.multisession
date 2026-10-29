using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal static class GeminiFixtureData
{
    internal static ProviderSessionContext Context() => new(Guid.NewGuid(), "opaque-deployment", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string> { ["system_instruction"] = "fixed instruction" });
    internal static ProviderBinding Binding() => new("gemini-interactions", 1, "interaction_root", "interaction_head");
    internal static ProviderBinding Lazy() => new("gemini-interactions", 1, null, null);
    internal static ProviderCall Call(string input = "new input", int outputBytes = 1024) => new(Context(), Guid.NewGuid(), Guid.NewGuid(), input, TimeSpan.FromSeconds(10), outputBytes);
    internal static string Json(string name)
    {
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gemini-v1.json")));
        return fixtures.RootElement.GetProperty(name).GetRawText();
    }
}
