using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal static class ProbeReplies
{
    internal static ProviderReply Success(ProviderCall call, ProviderBinding binding, string? output = null)
        => new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output ?? call.Input, true,
            binding with { ContinuationState = call.DispatchId.ToString("N") }, null);
    internal static ProviderReply Failed(ProviderBinding binding)
        => new(RequestState.Failed, ExecutionCertainty.ConfirmedEnded, "", false, binding,
            new(ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded))
        { ContinuationAvailable = true };
    internal static ProviderReply Cancelled(ProviderBinding binding)
        => new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, "", false, binding,
            new(ErrorCode.Cancelled, ExecutionCertainty.ConfirmedEnded));
}
