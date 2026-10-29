using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks acknowledged native evidence and public dispatch across the real storage boundary.</summary>
public sealed class DispatchCallbackTests
{
    private static readonly string[] _expectedInputs = ["first", "second"];
    private static readonly string[] _expectedHeads = ["confirmed", "next-head"];
    /// <summary>Observation is durable before return and never advances the confirmed head by itself.</summary>
    [Fact]
    public async Task StoresObservationBeforeAcknowledgementWithoutChangingBinding()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.CommitObservationAsync(fixture.Reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "opaque-operation"), CancellationToken.None);
        using JsonDocument envelope = JsonDocument.Parse((string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests")!);
        Assert.Equal("opaque-operation", envelope.RootElement.GetProperty("OperationReference").GetString());
        Assert.Equal(fixture.Session.Binding, (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.Binding);
        Assert.Equal(fixture.Session.BindingRevision, (await store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None))?.BindingRevision);
        await store.CommitCompletionAsync(fixture.Reservation.DispatchId, fixture.Reply(), CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            store.CommitObservationAsync(fixture.Reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "late-operation"), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
    }

    /// <summary>Malformed observations cannot alter the persisted dispatch envelope.</summary>
    [Theory]
    [InlineData(ProviderObservationKind.OperationAccepted, null)]
    [InlineData(ProviderObservationKind.SessionEstablished, "different-native-session")]
    [InlineData((ProviderObservationKind)99, "opaque")]
    public async Task RejectsInvalidObservationWithoutMutation(ProviderObservationKind kind, string? reference)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        string before = (string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests")!;
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            fixture.Host.Store.CommitObservationAsync(fixture.Reservation.DispatchId, new(kind, reference), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(before, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT ReservedDispatchJson FROM Requests"));
    }

    /// <summary>Provider I/O can acquire an independent write transaction and sees committed reservation and callbacks.</summary>
    [Fact]
    public async Task DispatchesOnlyNewInputsOutsideDatabaseTransaction()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        List<string> inputs = [];
        List<string?> heads = [];
        fixture.Provider.Sending = async (call, binding, observe, token) =>
        {
            inputs.Add(call.Input);
            heads.Add(binding.ContinuationState);
            Assert.Equal(1L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
            using (DatabaseWriteBlock available = new(fixture.Directory.DatabasePath)) { }
            await observe(new(ProviderObservationKind.OperationAccepted, "operation-" + call.DispatchId.ToString("N")), token);
            return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "output-" + call.Input, true, binding with { ContinuationState = "next-head" }, null);
        };
        RequestAdmission admission = new(fixture.Store, fixture.Options);
        await admission.SubmitAsync(new(session.SessionId, "first", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        await admission.SubmitAsync(new(session.SessionId, "second", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        RequestDispatcher dispatcher = new(fixture.Store, [new("connection-I", "fixture-adapter", 7, fixture.Provider)], fixture.Options);
        Assert.True(await dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.True(await dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.False(await dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(_expectedInputs, inputs);
        Assert.Equal(_expectedHeads, heads);
        Assert.Equal(2L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Results"));
    }
}
