using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Checks adapter output without performing execution, persistence or reservation release.</summary>
public static class ProviderReplyPolicy
{
    /// <summary>Normalizes invalid output without inventing text or external termination evidence.</summary>
    public static ProviderReply NormalizeOutput(ProviderReply reply, int maximumBytes)
    {
        PublicError? failure = Utf8MessageLimits.ValidateOutput(reply.Output, maximumBytes, reply.Certainty);
        if (failure?.Code is ErrorCode.InvalidConfiguration or ErrorCode.OutputLimitExceeded)
        {
            return Reject(reply, failure.Code);
        }

        if (reply.Error?.Code == ErrorCode.OutputLimitExceeded)
        {
            return Reject(reply, ErrorCode.OutputLimitExceeded);
        }

        if (reply.Outcome == RequestState.Succeeded && (failure is not null || !reply.OutputComplete))
        {
            return Reject(reply, ErrorCode.ExecutionFailed);
        }

        return reply;
    }

    private static ProviderReply Reject(ProviderReply reply, ErrorCode code)
        => reply with
        {
            Outcome = RequestState.Failed,
            Output = string.Empty,
            OutputComplete = false,
            Error = new PublicError(code, reply.Certainty, reply.Error?.SessionId, reply.Error?.RequestId),
        };
}
