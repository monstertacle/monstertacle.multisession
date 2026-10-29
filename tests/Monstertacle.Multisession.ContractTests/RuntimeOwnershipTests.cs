using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks store ownership, authoritative admission stop and observable worker faults.</summary>
public sealed class RuntimeOwnershipTests
{
    /// <summary>A second runtime cannot claim a store still owned by another runtime.</summary>
    [Fact]
    public async Task RejectsStoreReuseWithoutDisposingFirstOwnersLease()
    {
        InMemorySessionStore store = new();
        ControlledProvider provider = new();
        await using TestStateDirectoryLease firstLease = new(Options().StateDirectory);
        await using TestStateDirectoryLease secondLease = new(Options().StateDirectory);
        await using SessionRuntime first = Runtime(store, provider, firstLease);
        await using SessionRuntime second = Runtime(store, provider, secondLease);
        await first.StartAsync(Options(), CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => second.StartAsync(Options(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StoreAlreadyOwned, failure.Failure.Code);
        await second.DisposeAsync();
        Assert.True(firstLease.IsHeld);
        Assert.False(first.Completion.IsCompleted);
        Assert.Empty((await first.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items);
        await first.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    /// <summary>Stopping acceptance is authoritative inside the store transaction, even for an already entered caller.</summary>
    [Fact]
    public async Task StoreAdmissionStopRejectsLaterCommitsAsNotAccepted()
    {
        await using InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        ISessionStore writes = store;
        await writes.StartAsync(lease, CancellationToken.None);
        await writes.StopAcceptanceAsync(CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            writes.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Empty(store.Submissions);
    }
    /// <summary>Background infrastructure failures are observable, sanitized and conservative about external execution.</summary>
    [Fact]
    public async Task ExposesWorkerFailureAndReleasesOwnershipAfterWorkersEnd()
    {
        InMemorySessionStore store = new() { Reserving = token => throw new IOException("private storage details") };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        SessionRuntime runtime = Runtime(store, new(), lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        try
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.DoesNotContain("private", failure.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await Assert.ThrowsAsync<SessionOperationException>(() => runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask());
            await runtime.DisposeAsync();
        }
        Assert.True(lease.IsDisposed);
    }
}
