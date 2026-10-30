using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Admission preserves safe details and distinguishes its own timeout phases.</summary>
public sealed class AdmissionDiagnosticTests
{
    /// <summary>A commit failure retains lower-level codes, identities and known nonacceptance.</summary>
    [Fact]
    public async Task PreservesStoreFailureDetails()
    {
        FailureDetails details = new(FailureStage.StoreTransaction, 19, 1811);
        InMemorySessionStore store = new()
        {
            CommittingAcceptance = (_, _) => throw new SessionOperationException(new(ErrorCode.StorageUnavailable,
                AdmissionStatus: AdmissionStatus.NotAccepted))
            { Details = details },
        };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            admission.SubmitAsync(new(session.SessionId, "private-input-marker", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.Equal(details, failure.Details);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(session.SessionId, failure.Failure.SessionId);
        Assert.DoesNotContain("private-input-marker", JsonSerializer.Serialize(failure.Failure), StringComparison.Ordinal);
    }

    /// <summary>A monotonic deadline while waiting for commit identifies that phase without inventing native codes.</summary>
    [Fact]
    public async Task IdentifiesCommitTimeoutWithoutInventingNativeCause()
    {
        FakeTimeProvider clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            CommittingAcceptance = async (_, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
        };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions();
        RequestAdmission admission = new(store, options, clock);
        Task<RequestReceipt> pending = admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(options.AdmissionTimeout);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => pending);
        Assert.Equal(ErrorCode.AdmissionTimedOut, failure.Failure.Code);
        Assert.Equal(new FailureDetails(FailureStage.AdmissionCommit), failure.Details);
        Assert.Equal(AdmissionStatus.Unknown, failure.Failure.AdmissionStatus);
    }

    /// <summary>Unchanged errors preserve their JSON shape and old persisted errors remain readable.</summary>
    [Fact]
    public void MissingOptionalDetailsRemainCompatible()
    {
        PublicError old = new(ErrorCode.StorageUnavailable);
        string encoded = JsonSerializer.Serialize(old);
        Assert.DoesNotContain("Details", encoded, StringComparison.Ordinal);
        Assert.Equal(old, JsonSerializer.Deserialize<PublicError>(encoded));
        SessionOperationException enriched = new(old) { Details = new(FailureStage.StoreOpen, 14, 14) };
        Assert.Same(old, enriched.Failure);
        Assert.Equal(encoded, JsonSerializer.Serialize(enriched.Failure));
        Assert.NotNull(typeof(SessionOperationException).GetConstructor([typeof(PublicError)]));
        Assert.NotNull(typeof(AdmissionCancellationException).GetConstructor([typeof(PublicError), typeof(CancellationToken)]));
    }

    /// <summary>Caller cancellation preserves safe queue diagnostics and known nonacceptance.</summary>
    [Fact]
    public async Task PreservesCancellationDetailsAndToken()
    {
        using CancellationTokenSource caller = new();
        FailureDetails details = new(FailureStage.StoreQueue);
        InMemorySessionStore store = new()
        {
            CommittingAcceptance = (_, _) =>
            {
                caller.Cancel();
                throw new AdmissionCancellationException(new(ErrorCode.Cancelled, AdmissionStatus: AdmissionStatus.NotAccepted), caller.Token)
                { Details = details };
            },
        };
        SessionSnapshot session = await RequestAdmissionTests.SeedSessionAsync(store);
        RequestAdmission admission = new(store, RuntimeOptionsTests.ValidOptions());
        AdmissionCancellationException failure = await Assert.ThrowsAsync<AdmissionCancellationException>(() =>
            admission.SubmitAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), caller.Token).AsTask());
        Assert.Equal(details, failure.Details);
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
    }
}
