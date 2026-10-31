using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Host-neutral instances and lifetime ownership use only Common contracts and controlled doubles.</summary>
public sealed class HostCompositionTests
{
    /// <summary>Identical registration names in two instances select independent models, adapters and histories.</summary>
    [Fact]
    public async Task KeepsInstancesIsolatedAndAcceptsLaterInputAfterIdle()
    {
        await using IsolatedRuntimeHost a = new("a");
        await using IsolatedRuntimeHost b = new("b");
        await Task.WhenAll(a.StartAsync().AsTask(), b.StartAsync().AsTask());
        SessionSnapshot sessionA = await a.CreateSessionAsync();
        SessionSnapshot sessionB = await b.CreateSessionAsync();
        Assert.NotEqual(a.Store.Data.StoreId, b.Store.Data.StoreId);
        Assert.Equal("model-a", sessionA.Context.ModelId);
        Assert.Equal("model-b", sessionB.Context.ModelId);
        Assert.Equal("a", sessionA.Context.Settings["owner"]);
        Assert.Equal("b", sessionB.Context.Settings["owner"]);
        RequestSnapshot first = await a.SubmitAndCompleteAsync(sessionA.SessionId, "first");
        Assert.Equal("a:first", first.Result?.Output);
        Assert.Null(await b.Runtime.Sessions.GetSessionAsync(sessionA.SessionId, CancellationToken.None));
        Assert.Null(await b.Runtime.Requests.GetRequestAsync(first.RequestId, CancellationToken.None));
        Assert.Null(await a.Runtime.Sessions.GetSessionAsync(sessionB.SessionId, CancellationToken.None));
        Assert.Equal(sessionA.SessionId, Assert.Single((await a.Runtime.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items).SessionId);
        Assert.Equal(sessionB.SessionId, Assert.Single((await b.Runtime.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items).SessionId);
        Assert.Equal("a:later", (await a.SubmitAndCompleteAsync(sessionA.SessionId, "later")).Result?.Output);
        await a.Runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal("b:after-other-stop", (await b.SubmitAndCompleteAsync(sessionB.SessionId, "after-other-stop")).Result?.Output);
        Assert.False(b.Runtime.Completion.IsCompleted);
        Page<HistoryEntry> history = await b.Runtime.Sessions.ReadHistoryAsync(sessionB.SessionId, 100, null, CancellationToken.None);
        Assert.Equal("after-other-stop", Assert.Single(history.Items).Input);
    }

    /// <summary>Common composition references no platform library, database or hosting framework.</summary>
    [Fact]
    public void CommonHasNoPlatformOrHostingAssemblyDependencies()
    {
        string?[] names = typeof(SessionRuntime).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        foreach (string forbidden in new[] { "Monstertacle.Multisession.Windows", "Monstertacle.Multisession.Linux",
            "Monstertacle.Multisession.Storage.Sqlite", "Microsoft.Extensions.Hosting", "Microsoft.AspNetCore" })
        {
            Assert.DoesNotContain(names, name => name?.StartsWith(forbidden, StringComparison.Ordinal) == true);
        }
    }

    /// <summary>Stopping during an uncooperative open records uncertainty before releasing store ownership.</summary>
    [Fact]
    public async Task StopFinishesLocalOpeningBeforeReleasingOwnership()
    {
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        InMemorySessionStore store = new() { BlockingSession = () => Assert.True(lease.IsHeld) };
        DelayedOpeningProvider provider = new();
        await using SessionRuntime runtime = new(store, lease, [new("provider", "delayed", 1, provider)],
            [new("profile", "provider", "opaque-model", ExecutionKind.ModelResponse, new("frozen", new Dictionary<string, string>()))]);
        await runtime.StartAsync(Options(), CancellationToken.None);
        Task<SessionSnapshot> opening = runtime.Sessions.CreateSessionAsync("profile", new(null, null), CancellationToken.None).AsTask();
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            Assert.True(opening.IsCompleted);
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => opening);
            Assert.Equal(ErrorCode.Cancelled, failure.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.True(lease.IsDisposed);
            SessionSnapshot stored = Assert.Single(store.Data.Sessions.Values);
            Assert.Null(stored.Binding);
            Assert.Equal(SessionBlockReason.RecoveryRequired, stored.BlockReason);
        }
        finally
        {
            provider.Finish.TrySetResult(new("delayed", 1, "late-native-root", null));
            await Record.ExceptionAsync(() => opening);
        }
    }
}
