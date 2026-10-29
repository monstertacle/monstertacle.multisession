using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal static class OpenAiFixtureData
{
    internal static ProviderSessionContext Context() => new(Guid.NewGuid(), "opaque-model", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string> { ["instructions"] = "fixed instruction" });
    internal static ProviderBinding Binding() => new("openai-responses", 1, "conv_fixture", null);
    internal static ProviderCall Call(string input = "new input", int outputBytes = 1024)
        => new(Context(), Guid.NewGuid(), Guid.NewGuid(), input, TimeSpan.FromSeconds(10), outputBytes);
    internal static string Json(string name)
    {
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "openai-v1.json")));
        return fixtures.RootElement.GetProperty(name).GetRawText();
    }
}
