using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Injects real native SQL failures inside our fixture, without product fault-injection hooks.</summary>
public sealed class AcceptanceFailureTests
{
    /// <summary>The actual typed runtime path preserves native error evidence without a second admission or write.</summary>
    [Fact]
    public async Task TypedRuntimePreservesNativeFailureDetails()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.DisposeAsync();
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(fixture.Directory,
            [new("connection-I", "fixture-adapter", 7, fixture.Provider)]);
        StoreProbe.Execute(fixture.Directory.DatabasePath, "CREATE TRIGGER FailTypedAdmission AFTER UPDATE OF NextSequence ON Sessions BEGIN SELECT RAISE(FAIL,'private-typed-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            host.Runtime.Commands.DispatchAsync(new SubmitRequestCommand(new(session.SessionId, "private-input-marker", TimeSpan.FromSeconds(10), null, false))));
        Assert.Equal(new FailureDetails(FailureStage.StoreTransaction, 19, 1811), failure.Details);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.DoesNotContain("private-", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    /// <summary>A write followed by a trigger failure must roll back input and sequence together.</summary>
    [Fact]
    public async Task RollsBackPartialWritesBeforeReportingKnownNonAcceptance()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        StoreProbe.Execute(fixture.Directory.DatabasePath, """
            CREATE TRIGGER FailAdmission AFTER UPDATE OF NextSequence ON Sessions
            BEGIN SELECT RAISE(FAIL,'private-sql-marker'); END;
            """);
        RequestAdmission admission = new(fixture.Store, fixture.Options);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            admission.SubmitAsync(new(session.SessionId, "private-input-marker", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(new FailureDetails(FailureStage.StoreTransaction, 19, 1811), failure.Details);
        Assert.DoesNotContain("private-sql-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-input-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
    }

    /// <summary>A failure at COMMIT cannot be reclassified as definitely not accepted merely from a local exception.</summary>
    [Fact]
    public async Task ReportsUnknownCommitOutcomeWithLookupCandidate()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        StoreProbe.Execute(fixture.Directory.DatabasePath, """
            CREATE TABLE DeferredAdmissionProbe (
                RequestId TEXT,
                MissingSession TEXT REFERENCES Sessions(SessionId) DEFERRABLE INITIALLY DEFERRED
            );
            CREATE TRIGGER FailCommit AFTER INSERT ON Requests
            BEGIN INSERT INTO DeferredAdmissionProbe VALUES (NEW.RequestId,'missing-session'); END;
            """);
        RequestAdmission admission = new(fixture.Store, fixture.Options);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            admission.SubmitAsync(new(session.SessionId, "private-input-marker", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.Unknown, failure.Failure.AdmissionStatus);
        Assert.Equal(new FailureDetails(FailureStage.StoreCommit, 19, 787), failure.Details);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.Equal(session.SessionId, failure.Failure.SessionId);
        Assert.NotNull(failure.Failure.RequestId);
        Assert.NotEqual(Guid.Empty, failure.Failure.RequestId);
        Assert.DoesNotContain("private-input-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        Assert.Null(await fixture.Store.GetRequestAsync(failure.Failure.RequestId.Value, CancellationToken.None));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
    }
}
