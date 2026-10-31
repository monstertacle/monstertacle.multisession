using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Exercises bounded infrastructure work and cancellation against real SQLite locking.</summary>
public sealed class StorageFailureTests
{
    /// <summary>A failed acquisition of the write transaction cannot publish a receipt or consume sequence.</summary>
    [Fact]
    public async Task BusyBeforeTransactionReportsKnownNonAcceptance()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAcceptance acceptance = Candidate(session.SessionId);
        SessionOperationException failure;
        using (DatabaseWriteBlock block = new(fixture.Directory.DatabasePath))
        {
            failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.CommitAcceptanceAsync(acceptance, 100, CancellationToken.None).AsTask());
        }
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(nameof(ErrorCode.StorageUnavailable), failure.Message);
        Assert.Null(failure.InnerException);
        Assert.Null(await fixture.Store.GetRequestAsync(acceptance.RequestId, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
    }

    /// <summary>A full bounded channel rejects excess local work without pretending it reached durable admission.</summary>
    [Fact]
    public async Task RejectsExcessDatabaseWorkWhileWriterIsBusy()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync(dbCapacity: 1);
        SessionSnapshot session = await fixture.CreateSessionAsync();
        SessionOperationException[] failures;
        using (DatabaseWriteBlock block = new(fixture.Directory.DatabasePath))
        {
            Task<SessionOperationException>[] attempts = Enumerable.Range(0, 64).Select(_ => Assert.ThrowsAsync<SessionOperationException>(() =>
                fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, CancellationToken.None).AsTask())).ToArray();
            failures = await Task.WhenAll(attempts);
        }
        Assert.Contains(failures, failure => failure.Failure.Code == ErrorCode.LocalCapacityExceeded);
        Assert.All(failures, failure => Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    /// <summary>Cancellation of a job waiting behind busy work is immediately known not to have begun.</summary>
    [Fact]
    public async Task CancelsQueuedDatabaseWorkWithoutLosingAcceptanceCertainty()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAcceptance candidate = Candidate(session.SessionId);
        using CancellationTokenSource cancellation = new();
        using (DatabaseWriteBlock block = new(fixture.Directory.DatabasePath))
        {
            Task<RequestReceipt> leader = fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, CancellationToken.None).AsTask();
            Task<RequestReceipt> pending = fixture.Store.CommitAcceptanceAsync(candidate, 100, cancellation.Token).AsTask();
            long started = Stopwatch.GetTimestamp();
            cancellation.Cancel();
            AdmissionCancellationException failure = await Assert.ThrowsAsync<AdmissionCancellationException>(() => pending);
            Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<SessionOperationException>(() => leader);
        }
        Assert.Null(await fixture.Store.GetRequestAsync(candidate.RequestId, CancellationToken.None));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    /// <summary>A precancelled candidate and a postcommit cancellation have distinct durable outcomes.</summary>
    [Fact]
    public async Task DistinguishesBeforeWorkCancellationFromKnownCommit()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        using CancellationTokenSource before = new();
        before.Cancel();
        AdmissionCancellationException failure = await Assert.ThrowsAsync<AdmissionCancellationException>(() =>
            fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, before.Token).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        using CancellationTokenSource after = new();
        RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, after.Token);
        after.Cancel();
        Assert.Equal(1, receipt.Sequence);
        Assert.NotNull(await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None));
    }

    /// <summary>Disposal closes admission after active SQL work and prevents later use of the connection.</summary>
    [Fact]
    public async Task DisposesPendingWorkBeforeReleasingConnection()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        using (DatabaseWriteBlock block = new(fixture.Directory.DatabasePath))
        {
            Task<RequestReceipt> first = fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, CancellationToken.None).AsTask();
            Task<RequestReceipt> next = fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, CancellationToken.None).AsTask();
            await fixture.Store.DisposeAsync();
            await Assert.ThrowsAsync<SessionOperationException>(() => first);
            await Assert.ThrowsAsync<SessionOperationException>(() => next);
        }
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Store.CommitAcceptanceAsync(Candidate(session.SessionId), 100, CancellationToken.None).AsTask());
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.True(fixture.Directory.IsHeld);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    private static RequestAcceptance Candidate(Guid sessionId)
        => new(Guid.NewGuid(), new(sessionId, "only new input", TimeSpan.FromSeconds(10), null, false));
}
