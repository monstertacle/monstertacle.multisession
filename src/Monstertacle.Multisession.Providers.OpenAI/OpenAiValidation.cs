using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiValidation
{
    internal static Uri Endpoint(OpenAiOptions options)
    {
        Uri? endpoint = options.Endpoint;
        if (endpoint is null || !endpoint.IsAbsoluteUri || options.MaximumResponseBytes < 1 || string.IsNullOrWhiteSpace(options.CredentialReference)
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(options.AllowLocalHttp && endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
        {
            throw OpenAiFailure.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
        return new(endpoint.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }
    internal static void Context(ProviderSessionContext context)
    {
        if (context.ExecutionKind != ExecutionKind.ModelResponse || context.Settings.Keys.Any(key => key != "instructions"))
        {
            throw OpenAiFailure.Create(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.NotStarted);
        }
    }
    internal static void Binding(ProviderBinding binding)
    {
        if (binding.AdapterId != OpenAiProtocol.AdapterId || binding.FormatVersion != OpenAiProtocol.BindingVersion
            || string.IsNullOrWhiteSpace(binding.SessionReference))
        {
            throw OpenAiFailure.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
    }
    internal static void Call(ProviderCall request)
    {
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > RuntimeDefaults.MaximumTimerTimeout || request.MaxOutputBytes < 1)
        {
            throw OpenAiFailure.Create(ErrorCode.InvalidInput, ExecutionCertainty.NotStarted);
        }
    }
}
