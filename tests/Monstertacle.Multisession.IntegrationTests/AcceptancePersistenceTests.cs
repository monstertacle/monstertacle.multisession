using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks real commit boundaries through the host-neutral public facades and SQLite port.</summary>
public sealed class AcceptancePersistenceTests
{
    /// <summary>Native opening observes a committed local identity before its call.</summary>
    [Fact]
    public async Task CommitsOpeningBeforeNativeCallAndReturnsConfirmedMetadata()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        long? observedLifecycle = null;
        fixture.Provider.Opening = context => observedLifecycle = (long?)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT Lifecycle FROM Sessions");
        SessionSnapshot session = await fixture.CreateSessionAsync();
        Assert.Equal((long)SessionLifecycle.Opening, observedLifecycle);
        SessionSnapshot saved = (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionLifecycle.Open, saved.Lifecycle);
        Assert.Equal(session.SessionId, saved.SessionId);
        Assert.Equal(session.Binding, saved.Binding);
        Assert.Equal(session.CreatedUtc, saved.CreatedUtc);
        Assert.Equal(session.UpdatedUtc, saved.UpdatedUtc);
        Assert.Equal("revision-İ", saved.SettingsRevision);
        Assert.Equal("frozen-İ\n🙂", saved.Context.Settings["I"]);
        Assert.Null(saved.ClosedUtc);
    }

    /// <summary>Receipt corresponds to an already committed input without whitespace, Unicode or SQL reinterpretation.</summary>
    [Fact]
    public async Task ReceiptIdentifiesDurableExactInputAndQueuedSnapshot()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        const string input = "  new input\r\nα🙂 '; DROP TABLE Requests; --\n";
        RequestReceipt receipt = await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(Guid.NewGuid(), new(session.SessionId, input, TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestSnapshot saved = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.NotEqual(Guid.Empty, receipt.RequestId);
        Assert.Equal(1, receipt.Sequence);
        Assert.Equal(receipt.RequestId, saved.RequestId);
        Assert.Equal(receipt.Sequence, saved.Sequence);
        Assert.Equal(session.SessionId, saved.SessionId);
        Assert.Equal(RequestState.Queued, saved.State);
        Assert.Equal(ExecutionCertainty.NotStarted, saved.Certainty);
        Assert.Null(saved.Result);
        Assert.NotEqual(default, saved.AcceptedUtc);
        Assert.Equal(TimeSpan.Zero, saved.AcceptedUtc.Offset);
        Assert.Null(saved.StartedUtc);
        Assert.Null(saved.EndedUtc);
        Assert.Null(saved.CancellationRequestedUtc);
        using JsonDocument json = JsonDocument.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SubmissionJson FROM Requests")!);
        Assert.Equal(input, json.RootElement.GetProperty("Input").GetString());
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
    }

    /// <summary>Reopening retains accepted identities and continues the stored sequence.</summary>
    [Fact]
    public async Task ReopensQueuedRequestsWithoutReassigningIdentityOrSequence()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAcceptance first = new(Guid.NewGuid(), new(session.SessionId, "first", TimeSpan.FromSeconds(10), null, false));
        RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(first, 100, CancellationToken.None);
        await fixture.ReopenAsync();
        RequestSnapshot? original = await fixture.Store.GetRequestAsync(first.RequestId, CancellationToken.None);
        Assert.NotNull(original);
        Assert.Equal(receipt.Sequence, original.Sequence);
        Assert.Equal(RequestState.Queued, original.State);
        RequestReceipt next = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), first.Submission), 100, CancellationToken.None);
        Assert.Equal(2, next.Sequence);
        Assert.NotEqual(first.RequestId, next.RequestId);
        Assert.Equal(session.Binding, (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))?.Binding);
    }

    /// <summary>Twenty concurrent attempts share one durable capacity and sequence transaction.</summary>
    [Fact]
    public async Task AppliesQueueCapacityAndSequenceAtomicallyUnderConcurrency()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync(maxQueued: 3);
        SessionSnapshot session = await fixture.CreateSessionAsync();
        Task<(RequestReceipt? Receipt, ErrorCode? Error)>[] attempts = Enumerable.Range(0, 20).Select(async index =>
        {
            try { return ((RequestReceipt?)await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input-" + index, TimeSpan.FromSeconds(10), null, false)), 3, CancellationToken.None), (ErrorCode?)null); }
            catch (SessionOperationException failure) { Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus); return ((RequestReceipt?)null, (ErrorCode?)failure.Failure.Code); }
        }).ToArray();
        var results = await Task.WhenAll(attempts);
        RequestReceipt[] accepted = results.Where(item => item.Receipt is not null).Select(item => item.Receipt!).ToArray();
        Assert.Equal(3, accepted.Length);
        Assert.Equal(17, results.Count(item => item.Error == ErrorCode.QueueFull));
        Assert.Equal(Enumerable.Range(1, 3).Select(value => (long)value), accepted.Select(item => item.Sequence).Order());
        Assert.Equal(3, accepted.Select(item => item.RequestId).Distinct().Count());
        Assert.Equal(3L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(4L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
    }

    /// <summary>Same input is a distinct new submission; there is no implicit deduplication.</summary>
    [Fact]
    public async Task DoesNotDeduplicateIdenticalMessages()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAdmission admission = new(fixture.Store, fixture.Options);
        SubmitRequest submission = new(session.SessionId, " \r\n", TimeSpan.FromSeconds(10), null, false);
        RequestReceipt first = await admission.SubmitAsync(Guid.NewGuid(), submission, CancellationToken.None);
        RequestReceipt second = await admission.SubmitAsync(Guid.NewGuid(), submission, CancellationToken.None);
        Assert.NotEqual(first.RequestId, second.RequestId);
        Assert.Equal(first.Sequence + 1, second.Sequence);
    }

    /// <summary>Direct store callers cannot bypass input validation or consume a sequence on rejection.</summary>
    [Theory]
    [InlineData("", ErrorCode.InvalidInput)]
    [InlineData("🙂a", ErrorCode.InputLimitExceeded)]
    public async Task RejectsInvalidInputWithoutPartialEffect(string input, ErrorCode expected)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync(maxInputBytes: 4);
        SessionSnapshot session = await fixture.CreateSessionAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, input, TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None).AsTask());
        Assert.Equal(expected, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
        RequestReceipt accepted = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "🙂", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        Assert.Equal(1, accepted.Sequence);
    }

    /// <summary>A candidate cannot be empty or overwrite a previously accepted identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusesInvalidOrDuplicateCandidate(bool duplicate)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        Guid candidate = duplicate ? Guid.NewGuid() : Guid.Empty;
        RequestAcceptance acceptance = new(candidate, new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false));
        if (duplicate) { await fixture.Store.CommitAcceptanceAsync(acceptance, 100, CancellationToken.None); }
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.CommitAcceptanceAsync(acceptance, 100, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(duplicate ? 1L : 0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Requests"));
        Assert.Equal(duplicate ? 2L : 1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT NextSequence FROM Sessions"));
    }

    /// <summary>Stopping acceptance has no hidden queued request deletion and refuses further commits.</summary>
    [Fact]
    public async Task StopAcceptanceRefusesNewInputAndPreservesExistingQueuedRequest()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestAcceptance acceptance = new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false));
        await fixture.Store.CommitAcceptanceAsync(acceptance, 100, CancellationToken.None);
        await fixture.Store.StopAcceptanceAsync(CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.CommitAcceptanceAsync(acceptance with { RequestId = Guid.NewGuid() }, 100, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionClosed, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Equal(RequestState.Queued, (await fixture.Store.GetRequestAsync(acceptance.RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Increasing the durable next sequence also updates the authoritative session timestamp.</summary>
    [Fact]
    public async Task UpdatesSessionTimestampTogetherWithSequenceAssignment()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        RequestSnapshot request = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        SessionSnapshot saved = (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))!;
        Assert.Equal(request.AcceptedUtc, saved.UpdatedUtc);
        Assert.Equal(session.CreatedUtc, saved.CreatedUtc);
        Assert.Equal(session.SettingsRevision, saved.SettingsRevision);
    }
}
