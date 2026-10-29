namespace Monstertacle.Multisession.ValidationHost;

internal static class ValidationScenarioCatalog
{
    internal static bool Contains(string scenario) => scenario is "deterministic" or "routing" or "failures" or "cancellation"
        or "shutdown" or "capacity" or "workspace" or "extensions" or "recovery" or "native-context" or "isolation"
        or "embedded-consumer" or "retention" or "soak" or "limits" or "live-openai" or "live-gemini";
    internal static bool IsLive(string scenario) => scenario is "live-openai" or "live-gemini";
}
