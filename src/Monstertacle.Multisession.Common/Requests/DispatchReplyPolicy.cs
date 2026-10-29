using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Checks terminal evidence and immutable binding identity before any completion commit.</summary>
public static class DispatchReplyPolicy
{
    /// <summary>Normalizes output, terminal evidence and identity against the already validated original binding.</summary>
    public static ProviderReply Normalize(ProviderReply reply, ProviderBinding original, string? observedSessionReference, int maximumOutputBytes)
    {
        reply = ProviderReplyPolicy.NormalizeOutput(ProviderFailurePolicy.NormalizeError(reply), maximumOutputBytes);
        bool terminal = RequestStateMachine.IsTerminal(reply.Outcome);
        if (!terminal || !RequestStateMachine.IsValid(reply.Outcome, reply.Certainty) || reply.Certainty is not (ExecutionCertainty.ConfirmedEnded or ExecutionCertainty.NotStarted))
        {
            return Uncertain(reply, reply.Error?.Code ?? ErrorCode.ExecutionFailed);
        }

        if (reply.Binding is { } binding
            && (binding.AdapterId != original.AdapterId || binding.FormatVersion != original.FormatVersion
                || (original.SessionReference is { } stable && binding.SessionReference != stable)
                || (observedSessionReference is { } observed && binding.SessionReference != observed)))
        {
            return Uncertain(reply, ErrorCode.ProviderUnavailable);
        }

        if (reply.Outcome == RequestState.Succeeded && original.SessionReference is null
            && (observedSessionReference is null || reply.Binding?.SessionReference != observedSessionReference))
        {
            return Uncertain(reply, ErrorCode.ProviderUnavailable);
        }

        return reply.Certainty == ExecutionCertainty.NotStarted ? reply with { Binding = null } : reply;
    }

    private static ProviderReply Uncertain(ProviderReply reply, ErrorCode code) => reply with
    {
        Outcome = RequestState.Interrupted,
        Certainty = ExecutionCertainty.Unknown,
        Output = string.Empty,
        OutputComplete = false,
        Binding = null,
        Error = new PublicError(code, ExecutionCertainty.Unknown),
    };
}
