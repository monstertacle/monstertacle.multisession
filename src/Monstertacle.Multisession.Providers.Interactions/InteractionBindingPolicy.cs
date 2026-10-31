using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

internal static class InteractionBindingPolicy
{
    internal static bool IsLazy(ProviderBinding binding) => binding.SessionReference is null && binding.ContinuationState is null;
    internal static void Validate(ProviderBinding binding, bool allowUnconfirmedHead = false)
    {
        if (binding.AdapterId != InteractionProtocol.AdapterId || binding.FormatVersion != InteractionProtocol.BindingVersion
            || (binding.SessionReference is null && binding.ContinuationState is not null))
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
        if (!allowUnconfirmedHead && !IsLazy(binding) && string.IsNullOrWhiteSpace(binding.ContinuationState))
        {
            throw NativeHttpErrors.Create(ErrorCode.ProviderSessionExpired, ExecutionCertainty.NotStarted);
        }
    }
    internal static void Context(ProviderSessionContext context)
    {
        if (context.ExecutionKind != ExecutionKind.ModelResponse || context.Settings.Keys.Any(key => key != "system_instruction"))
        {
            throw NativeHttpErrors.Create(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.NotStarted);
        }
    }
    internal static Uri Endpoint(InteractionOptions options)
    {
        Uri endpoint = NativeHttpEndpoint.Normalize(options.Endpoint, options.CredentialReference, options.MaximumResponseBytes, options.AllowLocalHttp);
        if (endpoint.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("v1beta", StringComparer.Ordinal))
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
        return endpoint;
    }
    internal static void Call(ProviderCall request)
    {
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > RuntimeDefaults.MaximumTimerTimeout || request.MaxOutputBytes < 1)
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidInput, ExecutionCertainty.NotStarted);
        }
    }
}
