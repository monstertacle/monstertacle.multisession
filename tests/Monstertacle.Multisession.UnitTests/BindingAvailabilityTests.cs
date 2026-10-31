using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>External termination may release a slot while the session lacks usable continuation.</summary>
public sealed class BindingAvailabilityTests
{
    /// <summary>Known end/no-start plus an explicit continuation blocker commits result and block together.</summary>
    [Theory]
    [InlineData(ExecutionCertainty.ConfirmedEnded)]
    [InlineData(ExecutionCertainty.NotStarted)]
    public async Task BlocksFutureAdmissionAndDispatchWithoutHoldingEndedSlot(ExecutionCertainty certainty)
    {
        int sends = 0;
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "known-root", null),
            Sending = (call, binding, observe, token) =>
            {
                sends++;
                return ValueTask.FromResult(new ProviderReply(RequestState.Failed, certainty, string.Empty, false, binding,
                    new PublicError(ErrorCode.ExecutionFailed, certainty))
                { ContinuationAvailable = false });
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt first = await scenario.SubmitAsync("first");
        RequestReceipt waiting = await scenario.SubmitAsync("waiting");
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(SessionBlockReason.ProviderUnavailable, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
        Assert.Empty(scenario.Store.Reservations);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(1, sends);
        Assert.Equal(RequestState.Queued, (await ((ISessionStore)scenario.Store).GetRequestAsync(waiting.RequestId, CancellationToken.None))?.State);
        RequestSnapshot? result = await ((ISessionStore)scenario.Store).GetRequestAsync(first.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Failed, result?.State);
        Assert.Equal(false, result?.Result?.ContinuationAvailable);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.SubmitAsync("new input").AsTask());
        Assert.Equal(ErrorCode.SessionBlocked, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
    }

    /// <summary>A null opaque continuation is valid for stable-session producers; no global binding-format inference.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public async Task KeepsLegacyAndExplicitlyAvailableContinuationsUsable(bool? available)
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "stable-session", null),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(DispatchScenario.Success(binding) with { ContinuationAvailable = available }),
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync("first");
        await scenario.SubmitAsync("second");
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(SessionBlockReason.None, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
    }

    /// <summary>A continuation blocker never supplies missing external end evidence.</summary>
    [Fact]
    public async Task KeepsUnknownReservationAndRecoveryBlock()
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "root", null),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(RequestState.Failed, ExecutionCertainty.Unknown,
                string.Empty, false, null, new PublicError(ErrorCode.ExecutionFailed, ExecutionCertainty.Unknown))
            { ContinuationAvailable = false }),
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync();
        await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Single(scenario.Store.Reservations);
        Assert.Equal(SessionBlockReason.RecoveryRequired, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
    }

    /// <summary>Legacy explicit expiry blocks its session without requiring the optional new flag.</summary>
    [Fact]
    public async Task BlocksNativeExpiry()
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "expired-root", null),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(new ProviderReply(RequestState.Failed, ExecutionCertainty.NotStarted,
                string.Empty, false, null, new PublicError(ErrorCode.ProviderSessionExpired))),
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        await scenario.SubmitAsync();
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(SessionBlockReason.ProviderUnavailable, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
        Assert.Empty(scenario.Store.Reservations);
    }

    /// <summary>An unavailable host registration is not retried by failing every later request in the same session.</summary>
    [Fact]
    public async Task BlocksMissingDispatchRegistration()
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        await scenario.SubmitAsync();
        RequestDispatcher dispatcher = new(scenario.Store, [], scenario.Options);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(SessionBlockReason.ProviderUnavailable, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
        Assert.Empty(scenario.Store.Reservations);
    }
}
