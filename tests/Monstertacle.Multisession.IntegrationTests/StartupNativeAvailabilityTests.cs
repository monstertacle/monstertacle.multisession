using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Sessions;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Existing native state is checked before persisted queued input becomes executable.</summary>
public sealed class StartupNativeAvailabilityTests
{
    /// <summary>Rejected or replaced bindings block only their own preserved request and never silently reopen.</summary>
    [Theory]
    [InlineData(ErrorCode.AuthenticationFailed)]
    [InlineData(ErrorCode.ProviderSessionExpired)]
    [InlineData(ErrorCode.ProviderUnavailable)]
    [InlineData(null)]
    public async Task RejectsUnavailableNativeBindingBeforeAnySend(ErrorCode? error)
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot affected = await seed.CreateSessionAsync();
        SessionCatalog catalog = new(seed.Store, [new("independent", "fixture-adapter", 7, seed.Provider)],
            [new("other", "independent", "opaque-model", ExecutionKind.ModelResponse, new("settings", new Dictionary<string, string>()))]);
        SessionSnapshot other = await catalog.CreateSessionAsync("other", new(null, null), CancellationToken.None);
        RequestReceipt held = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(affected.SessionId, "preserved", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        RequestReceipt flowing = await seed.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(other.SessionId, "independent", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await seed.Store.DisposeAsync();
        int affectedSends = 0;
        AdmissionProvider unavailable = new()
        {
            Opening = _ => throw new InvalidOperationException("No replacement opening is permitted."),
            Reopening = (_, binding, _) => error is { } code
                ? throw new SessionOperationException(new(code))
                : ValueTask.FromResult(binding with { SessionReference = "foreign-native-id" }),
            Sending = (_, binding, _, _) =>
            {
                Interlocked.Increment(ref affectedSends);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "must not run", true, binding, null));
            },
        };
        AdmissionProvider available = new()
        {
            Sending = (_, binding, _, _) => ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "completed", true, binding, null)),
        };
        await using SqliteRuntimeFixture host = await SqliteRuntimeFixture.OpenAsync(seed.Directory,
            [new("connection-I", "fixture-adapter", 7, unavailable), new("independent", "fixture-adapter", 7, available)]);
        Assert.Equal(RequestState.Succeeded, (await host.CompletedAsync(flowing.RequestId)).State);
        SessionSnapshot saved = (await host.Runtime.Sessions.GetSessionAsync(affected.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionBlockReason.ProviderUnavailable, saved.BlockReason);
        Assert.Equal(error ?? ErrorCode.ProviderSessionExpired, saved.AvailabilityError);
        Assert.Equal(affected.Binding, saved.Binding);
        Assert.Equal(RequestState.Queued, (await host.Runtime.Requests.GetRequestAsync(held.RequestId, CancellationToken.None))?.State);
        Assert.Equal(0, affectedSends);
        Assert.Equal(0L, StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE SessionId='" + affected.SessionId.ToString("D") + "'"));
    }
}
