using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Storage.Sqlite;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Factory and runtime cannot silently enforce different policy or resource limits.</summary>
public sealed class BootstrapOptionsTests
{
    /// <summary>A valid but different host record is refused before recovery, worker dispatch or data mutation.</summary>
    [Theory]
    [InlineData("retention")]
    [InlineData("input")]
    [InlineData("queue")]
    [InlineData("dbqueue")]
    [InlineData("request")]
    [InlineData("shutdown")]
    [InlineData("concurrency")]
    [InlineData("output")]
    [InlineData("admission")]
    [InlineData("busy")]
    public async Task RejectsDifferentEffectiveOptionsBeforeStarting(string changed)
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await seed.CreateSessionAsync();
        RequestReceipt receipt = await new RequestAdmission(seed.Store, seed.Options).SubmitAsync(
            Guid.NewGuid(), new(session.SessionId, "preserved", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
        await seed.Store.DisposeAsync();
        await using RetainedTestLease lease = new(seed.Directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, seed.Directory.TrustScopeId, seed.Options, CancellationToken.None);
        RuntimeOptions options = changed switch
        {
            "retention" => seed.Options with { Retention = new(false, TimeSpan.FromDays(7)) },
            "input" => seed.Options with { MaxInputBytes = seed.Options.MaxInputBytes + 1 },
            "queue" => seed.Options with { MaxQueuedRequests = seed.Options.MaxQueuedRequests + 1 },
            "dbqueue" => seed.Options with { DbWorkQueueCapacity = seed.Options.DbWorkQueueCapacity + 1 },
            "request" => seed.Options with { RequestTimeout = seed.Options.RequestTimeout + TimeSpan.FromSeconds(1) },
            "shutdown" => seed.Options with { ShutdownTimeout = seed.Options.ShutdownTimeout + TimeSpan.FromSeconds(1) },
            "output" => seed.Options with { MaxOutputBytes = seed.Options.MaxOutputBytes + 1 },
            "admission" => seed.Options with { AdmissionTimeout = seed.Options.AdmissionTimeout + TimeSpan.FromSeconds(1) },
            "busy" => seed.Options with { DbBusyTimeout = seed.Options.DbBusyTimeout + TimeSpan.FromSeconds(1) },
            _ => seed.Options with { MaxConcurrentRequests = seed.Options.MaxConcurrentRequests + 1 },
        };
        Assert.Null(options.Validate());
        int sends = 0;
        AdmissionProvider provider = new()
        {
            Sending = (call, binding, observe, token) =>
            {
                Interlocked.Increment(ref sends);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "unexpected", true, binding, null));
            },
        };
        string beforeSession = (string)StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions")!;
        string beforeRequest = (string)StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT SnapshotJson FROM Requests")!;
        await using SessionRuntime runtime = new(store, lease, [new("connection-I", "fixture-adapter", 7, provider)], []);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.StartAsync(options, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Equal(0, sends);
        Assert.True(lease.IsHeld);
        Assert.Equal(beforeSession, StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions"));
        Assert.Equal(beforeRequest, StoreProbe.Scalar(seed.Directory.DatabasePath, "SELECT SnapshotJson FROM Requests"));
        Assert.Equal(receipt.RequestId, (await store.GetRequestAsync(receipt.RequestId, CancellationToken.None))?.RequestId);
    }

    /// <summary>An explicit reopen may select new options, but its runtime must then use the same effective record.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartsMatchingReopenedOptionsWithCanonicalDirectory(bool equivalentSpelling)
    {
        await using AdmissionStoreFixture seed = await AdmissionStoreFixture.CreateAsync();
        await seed.Store.DisposeAsync();
        RuntimeOptions options = seed.Options with { MaxConcurrentRequests = 2, Retention = new(false, TimeSpan.FromDays(7)) };
        await using RetainedTestLease lease = new(seed.Directory);
        await using ISessionStore store = await SqliteStoreFactory.OpenExistingAsync(lease, seed.Directory.TrustScopeId, options, CancellationToken.None);
        await using SessionRuntime runtime = new(store, lease, [], []);
        RuntimeOptions hostOptions = equivalentSpelling ? options with { StateDirectory = Path.Combine(options.StateDirectory, ".") } : options;
        await runtime.StartAsync(hostOptions, CancellationToken.None);
        Assert.Empty((await runtime.Sessions.ListSessionsAsync(100, null, CancellationToken.None)).Items);
        await runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }
}
