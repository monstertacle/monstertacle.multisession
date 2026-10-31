using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks local acceptance certainty independently from native execution.</summary>
public sealed class AdmissionOutcomeContractTests
{
    private static async Task<(RequestAdmission Admission, SubmitRequest Request)> CreateAsync(InMemorySessionStore store, TimeProvider? clock = null)
    {
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        return (new(store, new() { StateDirectory = Path.GetTempPath(), Retention = new(true, null) }, clock),
            new(session.SessionId, "only new input", TimeSpan.FromSeconds(10), null, false));
    }

    /// <summary>Local invalid input proves nonacceptance without a receipt or external work.</summary>
    [Fact]
    public async Task ReportsLocalRejectionAsNotAccepted()
    {
        InMemorySessionStore store = new();
        var (admission, request) = await CreateAsync(store);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => admission.SubmitAsync(Guid.NewGuid(), request with { Input = string.Empty }, CancellationToken.None).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Empty(store.Submissions);
    }

    /// <summary>Pre-cancel retains the caller identity and token without starting a commit.</summary>
    [Fact]
    public async Task PreCancelledCallerIsNotAccepted()
    {
        InMemorySessionStore store = new();
        var (admission, request) = await CreateAsync(store);
        using CancellationTokenSource caller = new();
        caller.Cancel();
        Guid requestId = Guid.NewGuid();
        AdmissionCancellationException failure = await Assert.ThrowsAsync<AdmissionCancellationException>(() => admission.SubmitAsync(requestId, request, caller.Token).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(requestId, failure.Failure.RequestId);
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.Empty(store.Submissions);
    }

    /// <summary>A raw commit failure is Unknown; only an explicit rollback statement can establish NotAccepted.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinguishesUnknownFailureFromExplicitRollback(bool rollbackKnown)
    {
        RequestAcceptance? candidate = null;
        InMemorySessionStore store = new()
        {
            Accepting = (acceptance, token) =>
            {
                candidate = acceptance;
                if (rollbackKnown) { throw new SessionOperationException(new PublicError(ErrorCode.StorageUnavailable, AdmissionStatus: AdmissionStatus.NotAccepted)); }
                throw new IOException("private transaction details");
            },
        };
        var (admission, request) = await CreateAsync(store);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => admission.SubmitAsync(Guid.NewGuid(), request, CancellationToken.None).AsTask());
        Assert.NotNull(candidate);
        Assert.NotEqual(Guid.Empty, candidate.RequestId);
        Assert.Equal(candidate.RequestId, failure.Failure.RequestId);
        Assert.Equal(request, candidate.Submission);
        Assert.Equal(rollbackKnown ? AdmissionStatus.NotAccepted : AdmissionStatus.Unknown, failure.Failure.AdmissionStatus);
        Assert.Equal(rollbackKnown ? ExecutionCertainty.NotStarted : ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A known successful store response wins over a caller cancellation observed after commit.</summary>
    [Fact]
    public async Task ReturnsActualCandidateReceiptDespiteCancellationAfterKnownCommit()
    {
        using CancellationTokenSource caller = new();
        RequestAcceptance? candidate = null;
        InMemorySessionStore store = new()
        {
            Accepting = (acceptance, token) => { candidate = acceptance; return ValueTask.CompletedTask; },
            Accepted = receipt => caller.Cancel(),
        };
        var (admission, request) = await CreateAsync(store);
        RequestReceipt receipt = await admission.SubmitAsync(Guid.NewGuid(), request, caller.Token);
        Assert.NotNull(candidate);
        Assert.NotEqual(Guid.Empty, candidate.RequestId);
        Assert.Equal(candidate.RequestId, receipt.RequestId);
        Assert.Equal(1, receipt.Sequence);
        Assert.Equal(request, store.Submissions[receipt.RequestId]);
    }

    /// <summary>Late commit is discoverable under the original candidate, with no implicit resubmission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundsUncooperativeCommitAndFindsLateCommitUnderCandidate(bool timeout)
    {
        using DispatchClock clock = new();
        using CancellationTokenSource caller = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource committed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestAcceptance? candidate = null;
        int attempts = 0;
        InMemorySessionStore store = new()
        {
            IgnoreAcceptanceCancellation = true,
            Accepting = async (acceptance, token) =>
            {
                candidate = acceptance;
                Interlocked.Increment(ref attempts);
                entered.TrySetResult();
                await release.Task;
            },
            Accepted = receipt => committed.TrySetResult(),
        };
        var (admission, request) = await CreateAsync(store, clock);
        Task<RequestReceipt> pending = admission.SubmitAsync(Guid.NewGuid(), request, caller.Token).AsTask();
        Assert.Same(entered.Task, await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
        try
        {
            if (timeout)
            {
                Assert.Equal(TimeSpan.FromSeconds(5), clock.DueTime);
                clock.Fire();
                SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(ErrorCode.AdmissionTimedOut, failure.Failure.Code);
                Assert.Equal(AdmissionStatus.Unknown, failure.Failure.AdmissionStatus);
                Assert.Equal(candidate?.RequestId, failure.Failure.RequestId);
            }
            else
            {
                caller.Cancel();
                AdmissionCancellationException failure = await Assert.ThrowsAsync<AdmissionCancellationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(AdmissionStatus.Unknown, failure.Failure.AdmissionStatus);
                Assert.Equal(candidate?.RequestId, failure.Failure.RequestId);
            }
            Assert.NotNull(candidate);
            Assert.NotEqual(Guid.Empty, candidate.RequestId);
            Assert.Null(await admission.GetRequestAsync(candidate.RequestId, CancellationToken.None));
        }
        finally { release.TrySetResult(); }
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(candidate);
        Assert.NotNull(await admission.GetRequestAsync(candidate.RequestId, CancellationToken.None));
        Assert.Equal(1, attempts);
        Assert.Single(store.Submissions);
    }
    /// <summary>A colliding store candidate cannot merge a second input into an existing request.</summary>
    [Fact]
    public async Task RejectsCandidateCollisionWithoutMergingSubmissions()
    {
        InMemorySessionStore store = new();
        var (_, request) = await CreateAsync(store);
        Guid id = Guid.NewGuid();
        ISessionStore writes = store;
        RequestReceipt receipt = await writes.CommitAcceptanceAsync(new(id, request), 100, CancellationToken.None);
        Assert.Equal(id, receipt.RequestId);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            writes.CommitAcceptanceAsync(new(id, request with { Input = "different input" }), 100, CancellationToken.None).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(request, store.Submissions[id]);
        Assert.Single(store.Submissions);
    }
}
