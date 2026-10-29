using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks the complete local-state/external-evidence matrix and immutable final outcomes.</summary>
public sealed class RequestStateTests
{
    /// <summary>Every known combination, plus undefined enum values, has an explicit validity verdict.</summary>
    [Fact]
    public void ValidatesAllStateEvidencePairs()
    {
        foreach (RequestState state in Enum.GetValues<RequestState>().Append((RequestState)999))
        {
            ExecutionCertainty[] allowed = state switch
            {
                RequestState.Queued => [ExecutionCertainty.NotStarted],
                RequestState.Running => [ExecutionCertainty.InProgress],
                RequestState.Succeeded => [ExecutionCertainty.ConfirmedEnded],
                RequestState.Failed or RequestState.Cancelled or RequestState.TimedOut =>
                    [ExecutionCertainty.NotStarted, ExecutionCertainty.ConfirmedEnded, ExecutionCertainty.Unknown],
                RequestState.Interrupted => [ExecutionCertainty.Unknown, ExecutionCertainty.ConfirmedEnded],
                _ => [],
            };
            foreach (ExecutionCertainty certainty in Enum.GetValues<ExecutionCertainty>().Append((ExecutionCertainty)999))
            {
                Assert.Equal(allowed.Contains(certainty), RequestStateMachine.IsValid(state, certainty));
            }
        }
    }

    /// <summary>Terminal local state is separate from safe capacity release.</summary>
    [Fact]
    public void RecognizesOnlyDefinedFinalOutcomes()
    {
        foreach (RequestState state in Enum.GetValues<RequestState>().Append((RequestState)999))
        {
            bool expected = state is RequestState.Succeeded or RequestState.Failed or RequestState.Cancelled or RequestState.TimedOut or RequestState.Interrupted;
            Assert.Equal(expected, RequestStateMachine.IsTerminal(state));
        }
    }

    /// <summary>Only dispatch, queued cancellation, first finalization and same-outcome clarification advance.</summary>
    [Fact]
    public void EnforcesEveryForwardTransition()
    {
        var pairs = (from state in Enum.GetValues<RequestState>() from certainty in Enum.GetValues<ExecutionCertainty>() select (state, certainty)).ToArray();
        foreach (var (current, evidence) in pairs)
        {
            foreach (var (next, nextEvidence) in pairs)
            {
                bool validPairs = RequestStateMachine.IsValid(current, evidence) && RequestStateMachine.IsValid(next, nextEvidence);
                bool expected = validPairs && (current switch
                {
                    RequestState.Queued => next == RequestState.Running || (next == RequestState.Cancelled && nextEvidence == ExecutionCertainty.NotStarted),
                    RequestState.Running => RequestStateMachine.IsTerminal(next),
                    _ => current == next && evidence == ExecutionCertainty.Unknown && nextEvidence == ExecutionCertainty.ConfirmedEnded,
                });
                Assert.Equal(expected, RequestStateMachine.CanTransition(current, evidence, next, nextEvidence));
            }
        }
        Assert.False(RequestStateMachine.CanTransition((RequestState)999, ExecutionCertainty.Unknown, RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded));
        Assert.False(RequestStateMachine.CanTransition(RequestState.Running, ExecutionCertainty.InProgress, RequestState.Failed, (ExecutionCertainty)999));
    }

    /// <summary>Dispatch and explicit clarification advance without rewriting a final outcome.</summary>
    [Theory]
    [InlineData(RequestState.Queued, ExecutionCertainty.NotStarted, RequestState.Running, ExecutionCertainty.InProgress)]
    [InlineData(RequestState.Queued, ExecutionCertainty.NotStarted, RequestState.Cancelled, ExecutionCertainty.NotStarted)]
    [InlineData(RequestState.Running, ExecutionCertainty.InProgress, RequestState.Failed, ExecutionCertainty.Unknown)]
    [InlineData(RequestState.Running, ExecutionCertainty.InProgress, RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded)]
    [InlineData(RequestState.Interrupted, ExecutionCertainty.Unknown, RequestState.Interrupted, ExecutionCertainty.ConfirmedEnded)]
    [InlineData(RequestState.Cancelled, ExecutionCertainty.Unknown, RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded)]
    public void AllowsRequiredForwardTransition(RequestState current, ExecutionCertainty evidence, RequestState next, ExecutionCertainty nextEvidence)
        => Assert.True(RequestStateMachine.CanTransition(current, evidence, next, nextEvidence));

    /// <summary>An impossible Interrupted/NotStarted reply cannot be accepted as a safe release.</summary>
    [Fact]
    public void RejectsImpossibleInterruptedReply()
    {
        ProviderBinding binding = new("controlled", 1, "native", null);
        ProviderReply reply = new(RequestState.Interrupted, ExecutionCertainty.NotStarted, string.Empty, false, null, new(ErrorCode.Interrupted));
        ProviderReply normalized = DispatchReplyPolicy.Normalize(reply, binding, null, 100);
        Assert.Equal(ExecutionCertainty.Unknown, normalized.Certainty);
    }
}
