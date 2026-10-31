using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Typed commands retain catalog and revision-bound recovery behavior.</summary>
public sealed class CqrsMutationTests
{
    /// <summary>Create, close and delete use the same state observed by legacy readers.</summary>
    [Fact]
    public async Task CreatesClosesAndDeletesThroughTypedCommands()
    {
        ControlledProvider provider = new() { OpeningBinding = new("test-adapter", 1, "native", null) };
        await using SessionRuntime runtime = new(new InMemorySessionStore(), new TestStateDirectoryLease(Options().StateDirectory),
            [new("registered-adapter", "test-adapter", 1, provider)],
            [new("profile", "registered-adapter", "model", ExecutionKind.ModelResponse, new("fixed", new Dictionary<string, string>()))]);
        await runtime.StartAsync(Options(), CancellationToken.None);
        SessionSnapshot created = await runtime.Commands.DispatchAsync(new CreateSessionCommand(Guid.NewGuid(), "profile", new(null, null)));
        Assert.Equal(created, await runtime.Sessions.GetSessionAsync(created.SessionId, CancellationToken.None));
        await runtime.Commands.DispatchAsync(new CloseSessionCommand(created.SessionId));
        Assert.Equal(SessionLifecycle.Closed, (await runtime.Sessions.GetSessionAsync(created.SessionId, CancellationToken.None))?.Lifecycle);
        await runtime.Commands.DispatchAsync(new DeleteSessionCommand(created.SessionId));
        Assert.Null((await runtime.Queries.DispatchAsync(new GetSessionQuery(created.SessionId))).Value);
    }

    /// <summary>Inspection preserves the reservation and resolution remains explicitly revision-bound.</summary>
    [Fact]
    public async Task InspectsWithoutReleasingThenResolvesExplicitly()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening, Binding = null };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using SessionRuntime runtime = Runtime(store, new(), new(Options().StateDirectory));
        await runtime.StartAsync(Options(), CancellationToken.None);
        RecoveryTarget target = new(RecoveryTargetKind.SessionOpening, session.SessionId);
        RecoveryCaseSnapshot original = (await runtime.Queries.DispatchAsync(new GetRecoveryCaseQuery(target))).Value!;
        ProviderReply inspected = await runtime.Queries.DispatchAsync(new InspectRecoveryQuery(target));
        Assert.Equal(ErrorCode.UnsupportedProviderAction, inspected.Error?.Code);
        Assert.Equal(original, (await runtime.Queries.DispatchAsync(new GetRecoveryCaseQuery(target))).Value);
        RecoveryDecision close = new(Guid.NewGuid(), target, original.Revision, RecoveryDecisionKind.CloseSession, "operator close", "case evidence");
        await runtime.Commands.DispatchAsync(new ResolveRecoveryCommand(close));
        RecoveryCaseSnapshot closing = (await runtime.Queries.DispatchAsync(new GetRecoveryCaseQuery(target))).Value!;
        Assert.Equal(original.Revision + 1, closing.Revision);
        SessionOperationException stale = await Assert.ThrowsAsync<SessionOperationException>(() => runtime.Commands.DispatchAsync(
            new ResolveRecoveryCommand(close with { DecisionId = Guid.NewGuid() })));
        Assert.Equal(ErrorCode.RevisionConflict, stale.Failure.Code);
        await runtime.Commands.DispatchAsync(new ResolveRecoveryCommand(close with
        {
            DecisionId = Guid.NewGuid(),
            ExpectedRevision = closing.Revision,
            Kind = RecoveryDecisionKind.ConfirmEnded
        }));
        Assert.Empty((await runtime.Queries.DispatchAsync(new ListRecoveryCasesQuery(10))).Items);
    }
}
