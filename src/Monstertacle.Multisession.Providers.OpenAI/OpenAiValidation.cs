using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiValidation
{
    internal static Uri Endpoint(OpenAiOptions options)
        => NativeHttpEndpoint.Normalize(options.Endpoint, options.CredentialReference, options.MaximumResponseBytes, options.AllowLocalHttp);
    internal static void Context(ProviderSessionContext context)
    {
        if (context.ExecutionKind != ExecutionKind.ModelResponse || context.Settings.Keys.Any(key => key != "instructions"))
        {
            throw NativeHttpErrors.Create(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.NotStarted);
        }
    }
    internal static void Binding(ProviderBinding binding)
    {
        if (binding.AdapterId != OpenAiProtocol.AdapterId || binding.FormatVersion != OpenAiProtocol.BindingVersion
            || string.IsNullOrWhiteSpace(binding.SessionReference))
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
    }
    internal static void Call(ProviderCall request)
    {
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > RuntimeDefaults.MaximumTimerTimeout || request.MaxOutputBytes < 1)
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidInput, ExecutionCertainty.NotStarted);
        }
    }
}
