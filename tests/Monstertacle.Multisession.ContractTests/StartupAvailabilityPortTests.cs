using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Model/credential availability remains a generic optional provider contract at startup.</summary>
public sealed class StartupAvailabilityPortTests
{
    /// <summary>Rejected availability is public before either reopening or dispatching preserved input.</summary>
    [Fact]
    public async Task BlocksRejectedAvailabilityBeforeReopen()
    {
        int reopens = 0;
        ControlledProvider provider = new()
        {
            CheckingAvailability = (_, _) => throw new SessionOperationException(new(ErrorCode.AuthenticationFailed)),
            Reopening = (_, binding, _) => { Interlocked.Increment(ref reopens); return ValueTask.FromResult(binding); },
        };
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, provider, lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        SessionSnapshot saved = (await runtime.Sessions.GetSessionAsync(session.SessionId, CancellationToken.None))!;
        Assert.Equal(ErrorCode.AuthenticationFailed, saved.AvailabilityError);
        Assert.Equal(SessionBlockReason.ProviderUnavailable, saved.BlockReason);
        Assert.Equal(0, reopens);
        Assert.False(provider.Started.Task.IsCompleted);
        Assert.Equal(session.Binding, saved.Binding);
        await runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }
}
