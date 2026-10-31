using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

/// <summary>Defines local request states independently from evidence of external completion.</summary>
public static class RequestStateMachine
{
    /// <summary>Checks a state and its external execution evidence.</summary>
    public static bool IsValid(RequestState state, ExecutionCertainty certainty) => state switch
    {
        RequestState.Queued => certainty == ExecutionCertainty.NotStarted,
        RequestState.Running => certainty == ExecutionCertainty.InProgress,
        RequestState.Succeeded => certainty == ExecutionCertainty.ConfirmedEnded,
        RequestState.Failed or RequestState.Cancelled or RequestState.TimedOut =>
            certainty is ExecutionCertainty.NotStarted or ExecutionCertainty.ConfirmedEnded or ExecutionCertainty.Unknown,
        RequestState.Interrupted => certainty is ExecutionCertainty.Unknown or ExecutionCertainty.ConfirmedEnded,
        _ => false,
    };

    /// <summary>Reports whether a state represents a final local outcome, without implying safe capacity release.</summary>
    public static bool IsTerminal(RequestState state) => state is RequestState.Succeeded or RequestState.Failed
        or RequestState.Cancelled or RequestState.TimedOut or RequestState.Interrupted;

    /// <summary>Checks a single forward transition, including explicit clarification of an unknown end.</summary>
    public static bool CanTransition(RequestState current, ExecutionCertainty evidence, RequestState next, ExecutionCertainty nextEvidence)
    {
        if (!IsValid(current, evidence) || !IsValid(next, nextEvidence)) { return false; }
        return current switch
        {
            RequestState.Queued => next == RequestState.Running || (next == RequestState.Cancelled && nextEvidence == ExecutionCertainty.NotStarted),
            RequestState.Running => IsTerminal(next),
            _ => current == next && evidence == ExecutionCertainty.Unknown && nextEvidence == ExecutionCertainty.ConfirmedEnded,
        };
    }
}
