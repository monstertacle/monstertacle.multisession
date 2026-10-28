using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks that unsafe submissions fail before entering the store's acceptance operation.</summary>
public sealed class RequestAdmissionValidationTests
{
    /// <summary>Refuses unknown local identities without entering the acceptance transaction.</summary>
    [Fact]
    public async Task RejectsUnknownSessionBeforeCommit()
    {
        int commits = 0;
        InMemorySessionStore store = new() { CommittingAcceptance = (_, _) => { commits++; return ValueTask.CompletedTask; } };
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        Guid unknown = Guid.NewGuid();

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(new(unknown, "input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.NotFound, failure.Failure.Code);
        Assert.Equal(unknown, failure.Failure.SessionId);
        Assert.Equal(0, commits);
    }

    /// <summary>Does not accept work into opening, closing, closed or explicitly blocked sessions.</summary>
    [Theory]
    [InlineData(SessionLifecycle.Opening, SessionBlockReason.None, ErrorCode.SessionBlocked)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.RecoveryRequired, ErrorCode.SessionBlocked)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.ProviderUnavailable, ErrorCode.SessionBlocked)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.StorageFailure, ErrorCode.SessionBlocked)]
    [InlineData(SessionLifecycle.Closing, SessionBlockReason.None, ErrorCode.SessionClosed)]
    [InlineData(SessionLifecycle.Closed, SessionBlockReason.None, ErrorCode.SessionClosed)]
    public async Task RejectsUnavailableSessionBeforeCommit(SessionLifecycle lifecycle, SessionBlockReason reason, ErrorCode expected)
    {
        int commits = 0;
        InMemorySessionStore store = new() { CommittingAcceptance = (_, _) => { commits++; return ValueTask.CompletedTask; } };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store, lifecycle, reason);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());

        Assert.Equal(expected, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Equal(0, commits);
    }

    /// <summary>Reuses the inclusive UTF-8 rule instead of trimming or counting characters.</summary>
    [Theory]
    [InlineData(null, ErrorCode.InvalidInput)]
    [InlineData("", ErrorCode.InvalidInput)]
    [InlineData("😀x", ErrorCode.InputLimitExceeded)]
    public async Task RejectsInvalidInputBeforeCommit(string? input, ErrorCode expected)
    {
        int commits = 0;
        InMemorySessionStore store = new() { CommittingAcceptance = (_, _) => { commits++; return ValueTask.CompletedTask; } };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions() with { MaxInputBytes = 4 });

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(new(session.SessionId, input!, TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());

        Assert.Equal(expected, failure.Failure.Code);
        Assert.Equal(session.SessionId, failure.Failure.SessionId);
        Assert.Equal(0, commits);
    }

    /// <summary>The request's execution deadline must be positive, finite and schedulable.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-10_000L)]
    [InlineData(long.MaxValue)]
    public async Task RejectsInvalidExecutionDeadlineBeforeCommit(long ticks)
    {
        int commits = 0;
        InMemorySessionStore store = new() { CommittingAcceptance = (_, _) => { commits++; return ValueTask.CompletedTask; } };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromTicks(ticks), null, false), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(0, commits);
    }

    /// <summary>Requires an explicit write key consistent with the frozen session binding.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("different-workspace")]
    public async Task RejectsMissingOrConflictingWriteKeyBeforeCommit(string? key)
    {
        int commits = 0;
        InMemorySessionStore store = new() { CommittingAcceptance = (_, _) => { commits++; return ValueTask.CompletedTask; } };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), key, true), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(0, commits);
    }

    /// <summary>Rejects invalid runtime limits at composition time, not after accepting a request.</summary>
    [Fact]
    public void RejectsInvalidRuntimeConfiguration()
    {
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxQueuedRequests = 0 };

        SessionOperationException failure = Assert.Throws<SessionOperationException>(
            () => new RequestAdmission(new InMemorySessionStore(), options));

        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
    }
}
