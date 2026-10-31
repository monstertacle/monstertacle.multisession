using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Conversations;

internal static class ConversationValidation
{
    internal static Uri Endpoint(ConversationOptions options)
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
        if (binding.AdapterId != ConversationProtocol.AdapterId || binding.FormatVersion != ConversationProtocol.BindingVersion
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
