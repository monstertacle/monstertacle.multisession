using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Independent sessions retain usable capacity when other sessions fail.</summary>
public sealed class FailureIsolationTests
{
    private static readonly ErrorCode[] _codes = [ErrorCode.AuthenticationFailed, ErrorCode.ProviderRateLimited,
        ErrorCode.TimedOut, ErrorCode.ExecutionFailed, ErrorCode.ProviderContextLimit];

    /// <summary>Five confirmed failures and five successes each execute once with their frozen provider and input.</summary>
    [Fact]
    public async Task FiveConfirmedFailuresDoNotPreventFiveIndependentSuccesses()
    {
        int failedSends = 0;
        ControlledProvider failing = new()
        {
            BindingForContext = context => new("controlled", 1, $"native-{context.SessionId:D}", null),
            Sending = async (call, binding, observe, token) =>
            {
                Interlocked.Increment(ref failedSends);
                await Task.Yield();
                ErrorCode code = _codes[int.Parse(call.Input, CultureInfo.InvariantCulture)];
                return new(code == ErrorCode.TimedOut ? RequestState.TimedOut : RequestState.Failed,
                    ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, new(code, ExecutionCertainty.ConfirmedEnded));
            }
        };
        InMemorySessionStore store = new();
        DispatchLoadHost host = DispatchLoadHost.Create(store, failing, DispatchLoadHost.Provider(Task.CompletedTask), RuntimeOptionsTests.ValidOptions());
        List<RequestReceipt> failed = [];
        List<RequestReceipt> successful = [];
        for (int index = 0; index < 5; index++)
        {
            SessionSnapshot bad = await host.Catalog.CreateSessionAsync(Guid.NewGuid(), "one", new(Path.GetTempPath(), "bad-" + index), CancellationToken.None);
            SessionSnapshot good = await host.Catalog.CreateSessionAsync(Guid.NewGuid(), "two", new(Path.GetTempPath(), "good-" + index), CancellationToken.None);
            failed.Add(await host.Admission.SubmitAsync(Guid.NewGuid(), new(bad.SessionId, index.ToString(CultureInfo.InvariantCulture), TimeSpan.FromSeconds(5), null, false), CancellationToken.None));
            successful.Add(await host.Admission.SubmitAsync(Guid.NewGuid(), new(good.SessionId, "success-" + index, TimeSpan.FromSeconds(5), null, false), CancellationToken.None));
        }
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            while (await host.Dispatcher.TryDispatchAsync(CancellationToken.None)) { }
        }));
        for (int index = 0; index < 5; index++)
        {
            RequestSnapshot failure = (await ((ISessionStore)store).GetRequestAsync(failed[index].RequestId, CancellationToken.None))!;
            RequestSnapshot success = (await ((ISessionStore)store).GetRequestAsync(successful[index].RequestId, CancellationToken.None))!;
            Assert.Equal(_codes[index], failure.Result?.Error?.Code);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, failure.Certainty);
            Assert.Equal(RequestState.Succeeded, success.State);
            Assert.Equal("success-" + index, success.Result?.Output);
        }
        Assert.Equal(5, failedSends);
        Assert.Equal(10, store.DispatchState.History.Count);
        Assert.Empty(store.Reservations);
    }

    /// <summary>All unknown slots block new sends until one explicit confirmed resolution frees a slot.</summary>
    [Fact]
    public async Task UnknownOccupancyIsNotReleasedForAnIndependentQueuedSession()
    {
        int failedSends = 0;
        ControlledProvider failing = new()
        {
            BindingForContext = context => new("controlled", 1, $"native-{context.SessionId:D}", null),
            Sending = (call, binding, observe, token) =>
            {
                Interlocked.Increment(ref failedSends);
                return ValueTask.FromResult(new ProviderReply(RequestState.Failed, ExecutionCertainty.Unknown, string.Empty, false, null, new(ErrorCode.ExecutionFailed)));
            }
        };
        InMemorySessionStore store = new();
        DispatchLoadHost host = DispatchLoadHost.Create(store, failing, DispatchLoadHost.Provider(Task.CompletedTask), RuntimeOptionsTests.ValidOptions() with { MaxConcurrentRequests = 2 });
        SessionSnapshot first = await host.Catalog.CreateSessionAsync(Guid.NewGuid(), "one", new(Path.GetTempPath(), "first"), CancellationToken.None);
        SessionSnapshot second = await host.Catalog.CreateSessionAsync(Guid.NewGuid(), "one", new(Path.GetTempPath(), "second"), CancellationToken.None);
        SessionSnapshot independent = await host.Catalog.CreateSessionAsync(Guid.NewGuid(), "two", new(Path.GetTempPath(), "independent"), CancellationToken.None);
        RequestReceipt firstReceipt = await host.Admission.SubmitAsync(Guid.NewGuid(), new(first.SessionId, "first", TimeSpan.FromSeconds(5), null, false), CancellationToken.None);
        await host.Admission.SubmitAsync(Guid.NewGuid(), new(second.SessionId, "second", TimeSpan.FromSeconds(5), null, false), CancellationToken.None);
        await Assert.ThrowsAsync<SessionOperationException>(() => host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<SessionOperationException>(() => host.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        RequestReceipt queued = await host.Admission.SubmitAsync(Guid.NewGuid(), new(independent.SessionId, "independent", TimeSpan.FromSeconds(5), null, false), CancellationToken.None);
        Assert.False(await host.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(new(1, 2, 2), await ((ISessionStore)store).GetRuntimeStatisticsAsync(CancellationToken.None));
        Assert.Equal(RequestState.Queued, (await ((ISessionStore)store).GetRequestAsync(queued.RequestId, CancellationToken.None))!.State);
        RecoveryCaseSnapshot item = (await ((ISessionStore)store).GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, firstReceipt.RequestId), CancellationToken.None))!;
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "explicit end", "non-secret evidence"), first.Binding, CancellationToken.None);
        Assert.True(await host.Dispatcher.TryDispatchAsync(CancellationToken.None));
        Assert.Equal(RequestState.Succeeded, (await ((ISessionStore)store).GetRequestAsync(queued.RequestId, CancellationToken.None))!.State);
        Assert.Equal(RequestState.Failed, (await ((ISessionStore)store).GetRequestAsync(firstReceipt.RequestId, CancellationToken.None))!.State);
        Assert.Equal(2, failedSends);
        Assert.Single(store.Reservations);
    }
}
