using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Shared submission validation, independent from admission waiting and storage commit certainty.</summary>
public static class RequestSubmissionRules
{
    /// <summary>Checks the same frozen session and input rules at admission and within its storage transaction.</summary>
    public static PublicError? Validate(SubmitRequest request, SessionSnapshot? session, RuntimeOptions options)
    {
        if (session is null) { return Error(ErrorCode.NotFound, request); }
        if (session.Lifecycle is SessionLifecycle.Closing or SessionLifecycle.Closed) { return Error(ErrorCode.SessionClosed, request); }
        if (session.Lifecycle != SessionLifecycle.Open || session.BlockReason != SessionBlockReason.None) { return Error(ErrorCode.SessionBlocked, request); }
        if (Utf8MessageLimits.ValidateInput(request.Input, options.MaxInputBytes) is { } failure) { return failure with { SessionId = request.SessionId }; }
        if (!ExecutionDeadline.IsValid(request.Timeout)) { return Error(ErrorCode.InvalidInput, request); }
        if (request.WriteIntent && (string.IsNullOrWhiteSpace(request.WorkspaceKey)
            || (session.Context.ExecutionContext.WorkspaceKey is { } boundKey && !string.Equals(boundKey, request.WorkspaceKey, StringComparison.Ordinal))))
        {
            return Error(ErrorCode.InvalidInput, request);
        }
        return null;
    }

    private static PublicError Error(ErrorCode code, SubmitRequest request) => new(code, SessionId: request.SessionId);
}
