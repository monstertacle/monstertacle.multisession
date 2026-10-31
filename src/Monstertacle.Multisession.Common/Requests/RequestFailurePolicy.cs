using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Retains the local failure category independently of unknown external termination.</summary>
public static class RequestFailurePolicy
{
    /// <summary>Builds a final local failure without claiming that the external execution ended.</summary>
    public static ProviderReply Uncertain(PublicError failure)
    {
        RequestState outcome = failure.Code switch
        {
            ErrorCode.Cancelled => RequestState.Cancelled,
            ErrorCode.TimedOut => RequestState.TimedOut,
            ErrorCode.Interrupted => RequestState.Interrupted,
            _ => RequestState.Failed,
        };
        return new(outcome, ExecutionCertainty.Unknown, string.Empty, false, null, failure with { Certainty = ExecutionCertainty.Unknown });
    }
}
