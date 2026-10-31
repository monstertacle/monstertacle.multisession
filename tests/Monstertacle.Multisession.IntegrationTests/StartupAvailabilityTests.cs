using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Sessions;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Local availability is established before automatic workers can consume preserved input.</summary>
public sealed class StartupAvailabilityTests
{
    private static SessionCatalog Catalog(AdmissionStoreFixture fixture, string providerId)
        => new(fixture.Store, [new(providerId, "fixture-adapter", 7, fixture.Provider)],
            [new("profile", providerId, "opaque-model", ExecutionKind.ModelResponse, new("frozen", new Dictionary<string, string>()))]);

    /// <summary>A missing registration blocks its session while an independent session finishes under original request identity.</summary>
    [Fact]
    public async Task BlocksMissingProviderBeforeDispatchAndContinuesIndependentSession()
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot unavailable = await Catalog(seed, "missing").CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        SessionSnapshot independent = await Catalog(seed, "available").CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        RequestReceipt blocked = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(unavailable.SessionId, "blocked", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        RequestReceipt accepted = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(independent.SessionId, "independent", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await seed.Store.DisposeAsync();
        AdmissionProvider provider = new() { Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "completed", true, binding, null)) };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(seed.Directory, [new("available", "fixture-adapter", 7, provider)]);
        RequestSnapshot result = await host.CompletedAsync(accepted.RequestId);
        Assert.Equal(RequestState.Succeeded, result.State);
        SessionSnapshot session = (await host.Runtime.Sessions.GetSessionAsync(unavailable.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionBlockReason.ProviderUnavailable, session.BlockReason);
        Assert.True(session.ConfigurationUnavailable);
        Assert.Equal(RequestState.Queued, (await host.Runtime.Requests.GetRequestAsync(blocked.RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Restoring configuration resumes preserved queued input, while controlled-stop cancellations require a new explicit request.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepairsOnlyItsOwnConfigurationBlockAfterRegistrationReturns(bool controlledStop)
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await seed.CreateSessionAsync();
        RequestReceipt accepted = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "preserved", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        if (controlledStop)
        {
            await seed.Store.DisposeAsync();
            await using SqliteRuntimeFixture missing = await SqliteRuntimeFixture.OpenAsync(seed.Directory, []);
            Assert.True((await missing.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.ConfigurationUnavailable);
        }
        else
        {
            await seed.Store.CommitConfigurationAvailabilityAsync(session.SessionId, false, CancellationToken.None, ErrorCode.AuthenticationFailed);
            await seed.Store.DisposeAsync();
        }
        AdmissionProvider provider = new()
        {
            Opening = _ => throw new InvalidOperationException("Existing state must not be opened again."),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "continued", true, binding, null)),
        };
        await using SqliteRuntimeFixture restored = await SqliteRuntimeFixture.OpenAsync(seed.Directory, [new("connection-I", "fixture-adapter", 7, provider)]);
        if (controlledStop)
        {
            Assert.Equal(RequestState.Cancelled, (await restored.Runtime.Requests.GetRequestAsync(accepted.RequestId, CancellationToken.None))?.State);
            accepted = await restored.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "explicit new input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        }
        Assert.Equal(RequestState.Succeeded, (await restored.CompletedAsync(accepted.RequestId)).State);
        SessionSnapshot saved = (await restored.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))!;
        Assert.False(saved.ConfigurationUnavailable);
        Assert.Null(saved.AvailabilityError);
        Assert.Equal(SessionBlockReason.None, saved.BlockReason);
        Assert.Equal(session.Binding?.SessionReference, saved.Binding?.SessionReference);
    }

    /// <summary>Known local format or execution-kind mismatches are refused before creating a new attempt.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlocksIncompatibleLocalRegistrationBeforeReservation(bool format)
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await seed.CreateSessionAsync();
        RequestReceipt accepted = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await seed.Store.DisposeAsync();
        AdmissionProvider provider = new() { Capabilities = new(false, true, false, false, false) };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(seed.Directory, [new("connection-I", "fixture-adapter", format ? 8 : 7, format ? new AdmissionProvider() : provider)]);
        Assert.True((await host.Runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))?.ConfigurationUnavailable);
        Assert.Equal(RequestState.Queued, (await host.Runtime.Requests.GetRequestAsync(accepted.RequestId, CancellationToken.None))?.State);
        Assert.Equal(0L, StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT count(*) FROM Reservations"));
    }
}
