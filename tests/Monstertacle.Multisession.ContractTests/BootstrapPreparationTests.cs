using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Runtime preparation must await store option validation before recovery or execution.</summary>
public sealed class BootstrapPreparationTests
{
    /// <summary>Uncertain opening recovery cannot mutate state while configuration validation is still pending.</summary>
    [Fact]
    public async Task AwaitsConfigurationValidationBeforeRecoveryAndWorkers()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new()
        {
            ValidatingOptions = async (options, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); },
        };
        SessionSnapshot opening = SchedulingData.Session() with { Binding = null, Lifecycle = SessionLifecycle.Opening };
        await store.CreateSessionAsync(opening, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, new(), lease);
        Task starting = runtime.StartAsync(Options(), CancellationToken.None).AsTask();
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, starting).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(starting.IsCompleted);
            Assert.Equal(SessionBlockReason.None, store.FindSession(opening.SessionId)?.BlockReason);
            release.TrySetResult();
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(SessionBlockReason.RecoveryRequired, store.FindSession(opening.SessionId)?.BlockReason);
        }
        finally
        {
            release.TrySetResult();
            await Record.ExceptionAsync(() => starting);
        }
    }
}
