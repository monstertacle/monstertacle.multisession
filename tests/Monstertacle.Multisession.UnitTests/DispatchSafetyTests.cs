using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks conservative dispatch outcomes, binding identity and sanitized errors.</summary>
public sealed class DispatchSafetyTests
{
    private static ProviderReply Reply(string kind, ProviderBinding binding) => kind switch
    {
        "oversize" => DispatchScenario.Success(binding) with { Output = "too long" },
        "incomplete" => DispatchScenario.Success(binding) with { OutputComplete = false },
        "progress" => DispatchScenario.Success(binding) with { Outcome = RequestState.Running },
        "unknown" => DispatchScenario.Success(binding) with { Certainty = ExecutionCertainty.Unknown },
        "adapter" => DispatchScenario.Success(binding with { AdapterId = "different" }),
        "version" => DispatchScenario.Success(binding with { FormatVersion = 2 }),
        "session" => DispatchScenario.Success(binding with { SessionReference = "replacement" }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Only complete final evidence with compatible identity can release a reservation.</summary>
    [Theory]
    [InlineData("oversize", false)]
    [InlineData("incomplete", false)]
    [InlineData("progress", true)]
    [InlineData("unknown", true)]
    [InlineData("adapter", true)]
    [InlineData("version", true)]
    [InlineData("session", true)]
    public async Task RejectsInvalidProviderResults(string kind, bool uncertain)
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "stable", "initial"),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(Reply(kind, binding)),
        };
        RuntimeOptions options = RuntimeOptionsTests.ValidOptions() with { MaxOutputBytes = kind == "oversize" ? 3 : 100 };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, options: options);
        RequestReceipt receipt = await scenario.SubmitAsync();
        if (uncertain)
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.Single(scenario.Store.Reservations);
            Assert.Equal(SessionBlockReason.RecoveryRequired, scenario.Store.FindSession(scenario.Session.SessionId)?.BlockReason);
            Assert.Equal(scenario.Session.Binding, scenario.Store.FindSession(scenario.Session.SessionId)?.Binding);
            Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        }
        else
        {
            Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
            RequestSnapshot? request = await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
            Assert.Equal(RequestState.Failed, request?.State);
            Assert.Equal(string.Empty, request?.Result?.Output);
            Assert.False(request?.Result?.OutputComplete);
            Assert.Equal(kind == "oversize" ? ErrorCode.OutputLimitExceeded : ErrorCode.ExecutionFailed, request?.Result?.Error?.Code);
            Assert.Empty(scenario.Store.Reservations);
        }
    }

    /// <summary>An external exception is sanitized and cannot release session/workspace/slot or cause another send.</summary>
    [Fact]
    public async Task PreservesUnknownAfterProviderException()
    {
        int sends = 0;
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) =>
            {
                Interlocked.Increment(ref sends);
                throw new IOException("secret prompt or credential");
            },
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync(writeKey: "workspace");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.ExecutionFailed, failure.Failure.Code);
        Assert.Equal(receipt.RequestId, failure.Failure.RequestId);
        Assert.Equal(scenario.Session.SessionId, failure.Failure.SessionId);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain("secret", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal("workspace", Assert.Single(scenario.Store.Reservations).Value.WorkspaceKey);
        Assert.False(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(1, sends);
    }

    /// <summary>A missing or incompatible host registration fails before any provider send.</summary>
    [Theory]
    [InlineData("Connection", "controlled", 1)]
    [InlineData("connection", "replacement", 1)]
    [InlineData("connection", "controlled", 2)]
    public async Task RejectsChangedRegistrationBeforeSend(string providerId, string adapterId, int version)
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        RequestReceipt receipt = await scenario.SubmitAsync();
        RequestDispatcher dispatcher = new(scenario.Store, [new(providerId, adapterId, version, scenario.Provider)], scenario.Options);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Failure.Certainty);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        RequestSnapshot? request = await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Failed, request?.State);
        Assert.Equal(ExecutionCertainty.NotStarted, request?.Certainty);
        Assert.Empty(scenario.Store.Reservations);
    }
}
