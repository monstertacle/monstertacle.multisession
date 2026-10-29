using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Separates permitted provider categories from locally owned storage and admission failures.</summary>
public static class ProviderFailurePolicy
{
    /// <summary>Preserves only provider-facing categories; undefined and locally owned categories become execution failure.</summary>
    public static ErrorCode NormalizeCode(ErrorCode code) => code switch
    {
        ErrorCode.InvalidConfiguration or ErrorCode.InvalidInput or ErrorCode.InputLimitExceeded or ErrorCode.OutputLimitExceeded
            or ErrorCode.AuthenticationFailed or ErrorCode.ProviderRateLimited or ErrorCode.ProviderUnavailable
            or ErrorCode.ProviderSessionExpired or ErrorCode.ProviderContextLimit or ErrorCode.UnsupportedProviderAction
            or ErrorCode.ExecutionFailed or ErrorCode.Cancelled or ErrorCode.TimedOut or ErrorCode.Interrupted => code,
        _ => ErrorCode.ExecutionFailed,
    };

    /// <summary>A typed exception supplies a category, never evidence that external execution ended.</summary>
    public static ErrorCode Classify(Exception failure)
        => failure is SessionOperationException known ? NormalizeCode(known.Failure.Code) : ErrorCode.ExecutionFailed;

    /// <summary>Removes foreign local identities and admission metadata and uses the reply's execution evidence.</summary>
    public static ProviderReply NormalizeError(ProviderReply reply)
        => reply.Error is { } failure ? reply with { Error = new(NormalizeCode(failure.Code), reply.Certainty) } : reply;
}
