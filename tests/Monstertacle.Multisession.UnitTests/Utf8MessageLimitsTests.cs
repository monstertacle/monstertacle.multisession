using Monstertacle.Multisession.Contracts;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks bytes rather than characters and preserves independent execution evidence.</summary>
public sealed class Utf8MessageLimitsTests
{
    /// <summary>Rejects empty input before any external execution.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RejectsEmptyInput(string? input)
    {
        PublicError? failure = Utf8MessageLimits.ValidateInput(input, 4);

        Assert.Equal(ErrorCode.InvalidInput, failure?.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure?.Certainty);
    }

    /// <summary>Distinguishes exact inclusive boundaries from one-byte excess.</summary>
    [Theory]
    [InlineData("éé", false)]
    [InlineData("😀", false)]
    [InlineData("ééx", true)]
    [InlineData("😀x", true)]
    [InlineData(" ", false)]
    public void EnforcesInclusiveInputBytes(string input, bool tooLarge)
    {
        PublicError? failure = Utf8MessageLimits.ValidateInput(input, 4);

        Assert.Equal(tooLarge ? ErrorCode.InputLimitExceeded : null, failure?.Code);
    }

    /// <summary>Refuses empty output without inventing a successful response.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RejectsEmptyOutput(string? output)
    {
        PublicError? failure = Utf8MessageLimits.ValidateOutput(output, 4, ExecutionCertainty.ConfirmedEnded);

        Assert.Equal(ErrorCode.ExecutionFailed, failure?.Code);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, failure?.Certainty);
    }

    /// <summary>Does not turn an output-limit failure into proof of external termination.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.ConfirmedEnded)]
    [InlineData(ExecutionCertainty.Unknown)]
    public void PreservesCertaintyForOversizedOutput(ExecutionCertainty certainty)
    {
        PublicError? failure = Utf8MessageLimits.ValidateOutput("ééx", 4, certainty);

        Assert.Equal(ErrorCode.OutputLimitExceeded, failure?.Code);
        Assert.Equal(certainty, failure?.Certainty);
    }

    /// <summary>Accepts complete output exactly at its inclusive UTF-8 boundary.</summary>
    [Theory]
    [InlineData("éé")]
    [InlineData("😀")]
    public void AcceptsExactOutputLimit(string output)
    {
        Assert.Null(Utf8MessageLimits.ValidateOutput(output, 4, ExecutionCertainty.ConfirmedEnded));
    }

    /// <summary>Rejects invalid configuration rather than treating it as an input/output failure.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonpositiveInputByteLimits(int maximumBytes)
    {
        Assert.Equal(ErrorCode.InvalidConfiguration, Utf8MessageLimits.ValidateInput("x", maximumBytes)?.Code);
    }

    /// <summary>Rejects invalid output limits without silently accepting output.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonpositiveOutputByteLimits(int maximumBytes)
    {
        Assert.Equal(ErrorCode.InvalidConfiguration, Utf8MessageLimits.ValidateOutput("x", maximumBytes, ExecutionCertainty.Unknown)?.Code);
    }
}
