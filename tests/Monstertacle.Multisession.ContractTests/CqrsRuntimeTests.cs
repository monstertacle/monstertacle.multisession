using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Monstertacle.SharpQrs;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Typed access shares durable state and lifecycle with the session, request and recovery ports.</summary>
public sealed class CqrsRuntimeTests
{
    /// <summary>Typed submission admits once and both interfaces observe the same request.</summary>
    [Fact]
    public async Task TypedSubmissionAndPortReadsShareOneAdmission()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using SessionRuntime runtime = Runtime(store, new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        RequestReceipt receipt = await runtime.Commands.DispatchAsync(new SubmitRequestCommand(
            Guid.NewGuid(), new(session.SessionId, "typed input", TimeSpan.FromSeconds(10), null, false)));
        Assert.Equal(receipt.RequestId, (await runtime.Requests.GetRequestAsync(receipt.RequestId, CancellationToken.None))?.RequestId);
        Assert.Equal(receipt.RequestId, Assert.Single((await runtime.Queries.DispatchAsync(new ListRequestsQuery(session.SessionId, 10))).Items).RequestId);
        Assert.Equal(session.SessionId, (await runtime.Queries.DispatchAsync(new GetSessionQuery(session.SessionId))).Value?.SessionId);
        Assert.Equal(session.SessionId, Assert.Single((await runtime.Queries.DispatchAsync(new ListSessionsQuery(10))).Items).SessionId);
        Assert.NotNull(await runtime.Queries.DispatchAsync(new ReadHistoryQuery(session.SessionId, 10)));
        await runtime.Commands.DispatchAsync(new CancelRequestCommand(receipt.RequestId));
    }

    /// <summary>Missing entities produce a non-null envelope without changing the ports' null semantics.</summary>
    [Fact]
    public async Task MissingLookupsRemainExplicit()
    {
        await using SessionRuntime runtime = Runtime(new(), new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        Guid absent = Guid.NewGuid();
        Assert.Null((await runtime.Queries.DispatchAsync(new GetSessionQuery(absent))).Value);
        Assert.Null((await runtime.Queries.DispatchAsync(new GetRequestQuery(absent))).Value);
        Assert.Null((await runtime.Queries.DispatchAsync(new GetRecoveryCaseQuery(new(RecoveryTargetKind.RequestExecution, absent)))).Value);
        Assert.Empty((await runtime.Queries.DispatchAsync(new ListRecoveryCasesQuery(10))).Items);
        Assert.Null(await runtime.Sessions.GetSessionAsync(absent, CancellationToken.None));
    }

    /// <summary>Runtime consumers cannot mutate completed setup registrations.</summary>
    [Fact]
    public async Task ExposesExecutionOnlyAndRejectsUnknownMessages()
    {
        await using SessionRuntime runtime = Runtime(new(), new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        Assert.False(runtime.Commands is ICommandRegistration);
        Assert.False(runtime.Queries is IQueryRegistration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Commands.DispatchAsync(new UnknownCommand()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Queries.DispatchAsync(new UnknownQuery()));
    }

    /// <summary>Retained dispatchers preserve stopped-runtime rejection and registries are instance-local.</summary>
    [Fact]
    public async Task KeepsIsolationAndLifecycleProtections()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using SessionRuntime first = Runtime(store, new(), new(Options().StateDirectory));
        await using SessionRuntime second = Runtime(new(), new(), new(Options().StateDirectory));
        await first.StartAsync(Options(), CancellationToken.None);
        await second.StartAsync(Options(), CancellationToken.None);
        Assert.NotNull((await first.Queries.DispatchAsync(new GetSessionQuery(session.SessionId))).Value);
        Assert.Null((await second.Queries.DispatchAsync(new GetSessionQuery(session.SessionId))).Value);
        IQueryDispatcher retained = first.Queries;
        await first.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await Assert.ThrowsAsync<SessionOperationException>(() => retained.DispatchAsync(new GetSessionQuery(session.SessionId)));
        Assert.Null((await second.Queries.DispatchAsync(new GetSessionQuery(session.SessionId))).Value);
    }

    private sealed record UnknownCommand : ICommand<CommandAcknowledgement>;
    private sealed record UnknownQuery : IQuery<CommandAcknowledgement>;
}
