using Monstertacle.Multisession.Contracts;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Regression checks for type-safe error values without arbitrary provider text.</summary>
public sealed class PublicErrorTests
{
    /// <summary>Keeps the local category separate from external execution evidence.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.NotStarted)]
    [InlineData(ExecutionCertainty.InProgress)]
    [InlineData(ExecutionCertainty.ConfirmedEnded)]
    [InlineData(ExecutionCertainty.Unknown)]
    public void PreservesExplicitExecutionEvidence(ExecutionCertainty certainty)
    {
        Guid sessionId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        PublicError failure = new(ErrorCode.ExecutionFailed, certainty, sessionId, requestId);

        Assert.Equal(ErrorCode.ExecutionFailed, failure.Code);
        Assert.Equal(certainty, failure.Certainty);
        Assert.Equal(sessionId, failure.SessionId);
        Assert.Equal(requestId, failure.RequestId);
    }

    /// <summary>Defaults pre-acceptance errors to no external start, not unknown or success.</summary>
    [Fact]
    public void DefaultsToNotStartedWithoutInventedIdentifiers()
    {
        PublicError failure = new(ErrorCode.InvalidInput);

        Assert.Equal(ExecutionCertainty.NotStarted, failure.Certainty);
        Assert.Null(failure.SessionId);
        Assert.Null(failure.RequestId);
    }
}
