using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>Structural admission of qualified typed outcome facts; invalid facts never fall back to response text.</summary>
internal static class FrameworkOutcomeRules
{
    internal static bool IsAdmissible(FrameworkExecutionOutcome outcome, AgentResponse response)
    {
#pragma warning disable MEAI001
        if (response.ContinuationToken is not null || outcome.Output is null) { return false; }
#pragma warning restore MEAI001
        if (outcome.ErrorCode is { } code && !Enum.IsDefined(code)) { return false; }
        return outcome.State switch
        {
            RequestState.Succeeded => outcome.Certainty == ExecutionCertainty.ConfirmedEnded && outcome.OutputComplete
                && outcome.Output.Length > 0 && outcome.ErrorCode is null,
            RequestState.Failed => outcome.ErrorCode is { } failure && failure is not (ErrorCode.Cancelled or ErrorCode.TimedOut) && IsKnownFailure(outcome),
            RequestState.Cancelled => outcome.ErrorCode == ErrorCode.Cancelled && IsKnownFailure(outcome),
            RequestState.TimedOut => outcome.ErrorCode == ErrorCode.TimedOut && IsKnownFailure(outcome),
            _ => false,
        };
    }

    internal static ProviderReply ToReply(FrameworkExecutionOutcome outcome, ProviderBinding? continuation, int maximumBytes)
    {
        ProviderReply reply = new(outcome.State, outcome.Certainty, outcome.Output, outcome.OutputComplete, continuation,
            outcome.ErrorCode is { } code ? new PublicError(code, outcome.Certainty) : null)
        {
            ContinuationAvailable = outcome.Certainty switch
            {
                ExecutionCertainty.NotStarted => null,
                ExecutionCertainty.ConfirmedEnded => continuation is not null,
                _ => false,
            },
        };
        return ProviderReplyPolicy.NormalizeOutput(reply, maximumBytes);
    }

    // Without confirmed end there is neither captured output nor usable continuation to report.
    private static bool IsKnownFailure(FrameworkExecutionOutcome outcome) => outcome.Certainty switch
    {
        ExecutionCertainty.ConfirmedEnded => true,
        ExecutionCertainty.NotStarted => outcome.Output.Length == 0 && !outcome.OutputComplete,
        ExecutionCertainty.Unknown => outcome.Output.Length == 0 && !outcome.OutputComplete && !outcome.ContinuationAvailable,
        _ => false,
    };
}
