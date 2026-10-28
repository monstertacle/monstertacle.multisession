using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks that acceptance is a stored request, not an external execution or early acknowledgement.</summary>
public sealed class RequestAdmissionTests
{
    internal static async ValueTask<SessionSnapshot> SeedSessionAsync(
        InMemorySessionStore store,
        SessionLifecycle lifecycle = SessionLifecycle.Open,
        SessionBlockReason blockReason = SessionBlockReason.None)
    {
        Guid id = Guid.NewGuid();
        ProviderSessionContext context = new(id, "opaque-model", ExecutionKind.AgentTask,
            new(Path.GetTempPath(), "workspace"), new Dictionary<string, string>());
        SessionSnapshot session = new(id, "connection", "profile", context, new("controlled", 1, "ses_existing", null))
        {
            Lifecycle = lifecycle,
            BlockReason = blockReason,
        };
        await store.CreateSessionAsync(session, CancellationToken.None);
        return session;
    }

    /// <summary>Passes the host-prepared input unchanged and returns the store's own receipt.</summary>
    [Fact]
    public async Task AcceptsUnmodifiedInputWithNewIdentity()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        SubmitRequest request = new(session.SessionId, "é\r\nprepared host context", TimeSpan.FromSeconds(10), null, false);

        RequestReceipt? receipt = await admission.SubmitAsync(request, CancellationToken.None);

        Assert.NotNull(receipt);
        Assert.NotEqual(Guid.Empty, receipt.RequestId);
        Assert.Equal(1, receipt.Sequence);
        Assert.Equal(request, store.Submissions[receipt.RequestId]);
    }

    /// <summary>Waits for the atomic acceptance operation rather than returning while it is still pending.</summary>
    [Fact]
    public async Task WaitsForCommitBeforeReturningReceipt()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            CommittingAcceptance = async (_, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            },
        };
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        SubmitRequest request = new(session.SessionId, "new input", TimeSpan.FromSeconds(10), null, false);

        Task<RequestReceipt> pending = admission.SubmitAsync(request, CancellationToken.None).AsTask();
        Task first = await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(entered.Task, first);
        Assert.False(pending.IsCompleted);
        Assert.Empty(store.Submissions);
        release.TrySetResult();
        RequestReceipt receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(request, store.Submissions[receipt.RequestId]);
    }

    /// <summary>A caller cancellation after a known committed acceptance cannot erase its receipt.</summary>
    [Fact]
    public async Task KeepsCommittedReceiptWhenCallerCancelsAfterCommit()
    {
        using CancellationTokenSource cancellation = new();
        InMemorySessionStore store = new() { Accepted = _ => cancellation.Cancel() };
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        SubmitRequest request = new(session.SessionId, "new input", TimeSpan.FromSeconds(10), null, false);

        RequestReceipt? receipt = await admission.SubmitAsync(request, cancellation.Token);

        Assert.NotNull(receipt);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(request, store.Submissions[receipt.RequestId]);
    }

    /// <summary>Reads accepted queue state from the owning store, never another instance's request.</summary>
    [Fact]
    public async Task ReadsOnlyAcceptedRequestsInItsOwnStore()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        RequestAdmission other = new(new InMemorySessionStore(), RuntimeOptionsTests.ValidOptions());
        RequestReceipt receipt = await admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);

        RequestSnapshot? found = await admission.GetRequestAsync(receipt.RequestId, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(receipt.RequestId, found.RequestId);
        Assert.Equal(receipt.Sequence, found.Sequence);
        Assert.Equal(session.SessionId, found.SessionId);
        Assert.Equal(RequestState.Queued, found.State);
        Assert.Equal(ExecutionCertainty.NotStarted, found.Certainty);
        Assert.Null(found.Result);
        Assert.Null(await other.GetRequestAsync(receipt.RequestId, CancellationToken.None));
        Assert.Null(await admission.GetRequestAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>Propagates a cancelled lookup instead of claiming the request did not exist.</summary>
    [Fact]
    public async Task CancelsLookupBeforeClaimingNotFound()
    {
        RequestAdmission admission = new(new InMemorySessionStore(), RuntimeOptionsTests.ValidOptions());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => admission.GetRequestAsync(Guid.NewGuid(), cancellation.Token).AsTask());
    }

    /// <summary>Accepts a real write declaration at the inclusive UTF-8 and minimum deadline boundaries.</summary>
    [Fact]
    public async Task AcceptsWriteAtInclusiveInputBoundary()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions() with { MaxInputBytes = 4 });
        SubmitRequest request = new(session.SessionId, "😀", TimeSpan.FromTicks(1), "workspace", true);

        RequestReceipt receipt = await admission.SubmitAsync(request, CancellationToken.None);

        Assert.Equal(request, store.Submissions[receipt.RequestId]);
        Assert.Equal(RequestState.Queued, (await admission.GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Same text is a new submission, while sequence numbering belongs to each separate session.</summary>
    [Fact]
    public async Task PreservesPerSessionSequencesWithoutDeduplicatingInput()
    {
        InMemorySessionStore store = new();
        SessionSnapshot first = await SeedSessionAsync(store);
        SessionSnapshot second = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        SubmitRequest original = new(first.SessionId, "same prepared input", TimeSpan.FromSeconds(10), null, false);

        RequestReceipt one = await admission.SubmitAsync(original, CancellationToken.None);
        RequestReceipt two = await admission.SubmitAsync(original, CancellationToken.None);
        RequestReceipt other = await admission.SubmitAsync(original with { SessionId = second.SessionId }, CancellationToken.None);

        Assert.Equal(1, one.Sequence);
        Assert.Equal(2, two.Sequence);
        Assert.Equal(1, other.Sequence);
        Assert.NotEqual(one.RequestId, two.RequestId);
        Assert.NotEqual(two.RequestId, other.RequestId);
        Assert.Equal(3, store.Submissions.Count);
    }

    /// <summary>Passes one instance-wide queue capacity to the authoritative store instead of counting per session locally.</summary>
    [Fact]
    public async Task EnforcesConfiguredGlobalQueueCapacity()
    {
        InMemorySessionStore store = new();
        SessionSnapshot first = await SeedSessionAsync(store);
        SessionSnapshot second = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions() with { MaxQueuedRequests = 2 });
        SubmitRequest request = new(first.SessionId, "input", TimeSpan.FromSeconds(10), null, false);
        await admission.SubmitAsync(request, CancellationToken.None);
        await admission.SubmitAsync(request with { SessionId = second.SessionId }, CancellationToken.None);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => admission.SubmitAsync(request, CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.QueueFull, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.Equal(2, store.Submissions.Count);
    }

    /// <summary>A cancelled pending commit does not receive a success receipt or start external execution.</summary>
    [Fact]
    public async Task CancelsPendingAcceptanceWithoutIssuingReceipt()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            CommittingAcceptance = async (_, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            },
        };
        SessionSnapshot session = await SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        using CancellationTokenSource cancellation = new();
        Task<RequestReceipt> pending = admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(store.Submissions);
    }
}
