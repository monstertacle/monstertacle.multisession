using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks adapter data at the provider-neutral output boundary, without a live provider.</summary>
public sealed class ProviderReplyBoundaryTests
{
    /// <summary>An oversized response is not a truncated success or proof of termination.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.ConfirmedEnded)]
    [InlineData(ExecutionCertainty.Unknown)]
    public void RejectsOversizedOutputWithoutChangingEvidence(ExecutionCertainty certainty)
    {
        ProviderReply reply = new(RequestState.Succeeded, certainty, "😀x", true, null, null);

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, 4);

        AssertRejected(result, ErrorCode.OutputLimitExceeded, certainty);
        Assert.Equal("😀x", reply.Output);
        Assert.True(reply.OutputComplete);
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
    }

    /// <summary>A limit error from an adapter cannot retain a short preview as complete output.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.ConfirmedEnded)]
    [InlineData(ExecutionCertainty.Unknown)]
    public void ClearsPreviewOnReportedOutputLimit(ExecutionCertainty certainty)
    {
        ProviderBinding binding = new("third-contract", 1, "native-session", "confirmed-head");
        ProviderReply reply = new(RequestState.Failed, certainty, "part", true, binding,
            new(ErrorCode.OutputLimitExceeded, certainty));

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, 4);

        AssertRejected(result, ErrorCode.OutputLimitExceeded, certainty);
        Assert.Same(binding, result.Binding);
        Assert.Equal("part", reply.Output);
    }

    /// <summary>Empty or incomplete successful text cannot become a fabricated final answer.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("part", false)]
    public void RejectsEmptyOrIncompleteSuccess(string? output, bool complete)
    {
        ProviderReply reply = new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output!, complete, null, null);

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, 4);

        AssertRejected(result, ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded);
    }

    /// <summary>An invalid host limit is not an adapter output-limit violation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidByteLimit(int maximumBytes)
    {
        ProviderReply reply = new(RequestState.Failed, ExecutionCertainty.Unknown, "", false, null,
            new(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown));

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, maximumBytes);

        AssertRejected(result, ErrorCode.InvalidConfiguration, ExecutionCertainty.Unknown);
    }

    /// <summary>Complete output at the inclusive byte boundary is neither normalized nor trimmed.</summary>
    [Theory]
    [InlineData("😀")]
    [InlineData("éé")]
    [InlineData(" \r\n ")]
    public void PreservesCompleteOutputAndBinding(string output)
    {
        ProviderBinding binding = new("independent-adapter", 1, "opaque-session", "opaque-continuation");
        ProviderReply reply = new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output, true, binding, null);

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, 4);

        Assert.Same(reply, result);
        Assert.Equal(output, result.Output);
        Assert.Same(binding, result.Binding);
        Assert.Null(result.Error);
    }

    /// <summary>A legitimate failure without text remains a failure, not a reason for context replay.</summary>
    [Theory]
    [InlineData(RequestState.Failed, ErrorCode.ProviderContextLimit, ExecutionCertainty.ConfirmedEnded)]
    [InlineData(RequestState.Cancelled, ErrorCode.Cancelled, ExecutionCertainty.Unknown)]
    [InlineData(RequestState.TimedOut, ErrorCode.TimedOut, ExecutionCertainty.Unknown)]
    public void PreservesNonSuccessWithoutReplacementText(RequestState outcome, ErrorCode code, ExecutionCertainty certainty)
    {
        ProviderReply reply = new(outcome, certainty, string.Empty, false, null, new(code, certainty));

        Assert.Same(reply, ProviderReplyPolicy.NormalizeOutput(reply, 4));
    }

    /// <summary>Normalization does not erase safe local correlation or rewrite the original error.</summary>
    [Fact]
    public void PreservesCorrelationWhenRejectingOutput()
    {
        Guid sessionId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        PublicError original = new(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown, sessionId, requestId);
        ProviderReply reply = new(RequestState.Failed, ExecutionCertainty.Unknown, "ééx", false, null, original);

        ProviderReply result = ProviderReplyPolicy.NormalizeOutput(reply, 4);

        AssertRejected(result, ErrorCode.OutputLimitExceeded, ExecutionCertainty.Unknown);
        Assert.Equal(sessionId, result.Error?.SessionId);
        Assert.Equal(requestId, result.Error?.RequestId);
        Assert.Same(original, reply.Error);
        Assert.Equal(ErrorCode.ProviderUnavailable, original.Code);
    }

    private static void AssertRejected(ProviderReply result, ErrorCode expected, ExecutionCertainty certainty)
    {
        Assert.Equal(RequestState.Failed, result.Outcome);
        Assert.Equal(string.Empty, result.Output);
        Assert.False(result.OutputComplete);
        Assert.Equal(certainty, result.Certainty);
        Assert.Equal(expected, result.Error?.Code);
        Assert.Equal(certainty, result.Error?.Certainty);
    }
}
