using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Dispatch preserves cancellation, tokens and handler failures.</summary>
public sealed class CqrsFailureTests
{
    /// <summary>Cancellation remains cooperative and errors preserve the established public contract.</summary>
    [Fact]
    public async Task PreservesCancellationAndTypedErrors()
    {
        await using SessionRuntime runtime = Runtime(new(), new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.Commands.DispatchAsync(
            new CreateSessionCommand("missing", new(null, null)), cancelled.Token));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Commands.DispatchAsync(
            new CreateSessionCommand("missing", new(null, null))));
        Assert.Equal(ErrorCode.NotFound, failure.Failure.Code);
    }

    /// <summary>Dispatch forwards the caller token and original handler exception without replacement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreservesQueryTokenAndExceptionIdentity(bool synchronous)
    {
        using CancellationTokenSource caller = new();
        CancellationToken observed = default;
        InvalidOperationException original = new("controlled store failure");
        InMemorySessionStore store = new()
        {
            ReadingRequest = (_, token) =>
            {
                observed = token;
                return synchronous ? throw original : ValueTask.FromException(original);
            },
        };
        await using SessionRuntime runtime = Runtime(store, new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.Queries.DispatchAsync(new GetRequestQuery(Guid.NewGuid()), caller.Token));
        Assert.Same(original, failure);
        Assert.Equal(caller.Token, observed);
    }

}
