using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Typed invocation context: preparation, actual acceptance and native session evidence are separate and bounded.</summary>
public sealed class FrameworkInvocationTests
{
    private static AgentResponse Success(string text = "done") => new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Stop };

    private static FrameworkInvocation Required(AgentRunOptions? options)
    {
        Assert.True(FrameworkInvocation.TryGet(options, out FrameworkInvocation? invocation));
        return invocation!;
    }

    /// <summary>Agents report native session and accepted operation through awaited typed callbacks linked to the SDK identities.</summary>
    [Fact]
    public async Task ReportsLinkedEvidenceInOrderWithoutReplacingBinding()
    {
        List<ProviderObservation> seen = [];
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderCall call = FrameworkSessionProviderTests.Call(context);
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                FrameworkInvocation invocation = Required(options);
                Assert.Equal((context.SessionId, call.RequestId, call.DispatchId), (invocation.SessionId, invocation.RequestId, invocation.DispatchId));
                await invocation.ObserveNativeSessionAsync("native-1", token);
                await invocation.ObserveOperationAcceptedAsync("operation-1", token);
                await invocation.ReportProgressAsync(token);
                return Success();
            },
        };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderBinding opened = await provider.OpenAsync(context, default);
        ProviderReply reply = await provider.SendAsync(call, opened, (observation, _) => { seen.Add(observation); return ValueTask.CompletedTask; }, default);
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.Equal(opened.SessionReference, reply.Binding?.SessionReference);
        Assert.Equal(
            [new(ProviderObservationKind.InvocationPrepared, call.DispatchId.ToString("N")), new(ProviderObservationKind.NativeSessionObserved, "native-1"),
             new(ProviderObservationKind.OperationAccepted, "operation-1"), new(ProviderObservationKind.Progress, null)],
            seen);
    }

    /// <summary>A failed evidence acknowledgement cannot be overtaken by a later successful response.</summary>
    [Fact]
    public async Task FailedObservationCannotBeOvertakenBySuccess()
    {
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                try { await Required(options).ObserveNativeSessionAsync("native-1", token); }
                catch (IOException) { }
                return Success();
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderBinding opened = await provider.OpenAsync(context, default);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), opened,
            (observation, _) => observation.Kind == ProviderObservationKind.NativeSessionObserved ? throw new IOException("private") : ValueTask.CompletedTask, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Empty(reply.Output);
        Assert.False(reply.ContinuationAvailable);
        Assert.Null(reply.Binding);
    }

    /// <summary>Callbacks after the invocation returned are refused and never reach durable storage.</summary>
    [Fact]
    public async Task LateObservationIsRefused()
    {
        FrameworkInvocation? retained = null;
        List<ProviderObservation> seen = [];
        FrameworkTestAgent agent = new() { RunningWith = (options, _) => { retained = Required(options); return Task.FromResult(Success()); } };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default),
            (observation, _) => { seen.Add(observation); return ValueTask.CompletedTask; }, default);
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => retained!.ObserveNativeSessionAsync("late", default).AsTask());
        Assert.Equal(ProviderObservationKind.InvocationPrepared, Assert.Single(seen).Kind);
    }

    /// <summary>The number of agent-reported observations is bounded; exceeding it makes the outcome uncertain.</summary>
    [Fact]
    public async Task ObservationCountIsBounded()
    {
        int refused = 0;
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                FrameworkInvocation invocation = Required(options);
                for (int index = 0; index < 3; index++)
                {
                    try { await invocation.ReportProgressAsync(token); }
                    catch (InvalidOperationException) { refused++; }
                }
                return Success();
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent, new() { AdapterId = "framework-contract-v1", MaximumObservations = 2 });
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default), FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(1, refused);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
    }

    /// <summary>Malformed identities are refused locally before any durable callback.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task BlankIdentityIsRefused(string identity)
    {
        List<ProviderObservation> seen = [];
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                await Assert.ThrowsAsync<ArgumentException>(() => Required(options).ObserveNativeSessionAsync(identity, token).AsTask());
                return Success();
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default),
            (observation, _) => { seen.Add(observation); return ValueTask.CompletedTask; }, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Single(seen);
    }

    /// <summary>A hung acknowledgement is bounded by the dispatch deadline and leaves the outcome uncertain.</summary>
    [Fact]
    public async Task HungAcknowledgementIsBoundedByDeadline()
    {
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                await Required(options).ObserveOperationAcceptedAsync("operation", token);
                return Success();
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderCall call = FrameworkSessionProviderTests.Call(context) with { Timeout = TimeSpan.FromMilliseconds(200) };
        ProviderReply reply = await provider.SendAsync(call, await provider.OpenAsync(context, default),
            async (observation, _) => { if (observation.Kind == ProviderObservationKind.OperationAccepted) { await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None); } }, default)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.TimedOut, reply.Error?.Code);
    }

    /// <summary>A typed not-started fact contradicting acknowledged external evidence is uncertain, never not-invoked.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NotStartedAfterExternalEvidenceIsUnknown(bool operation)
    {
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                FrameworkInvocation invocation = Required(options);
                if (operation) { await invocation.ObserveOperationAcceptedAsync("operation", token); }
                else { await invocation.ObserveNativeSessionAsync("native", token); }
                return new AgentResponse(new ChatMessage(ChatRole.Assistant, string.Empty))
                {
                    RawRepresentation = new FrameworkExecutionOutcome { State = RequestState.Failed, Certainty = ExecutionCertainty.NotStarted, ErrorCode = ErrorCode.ProviderUnavailable },
                };
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent, new() { AdapterId = "framework-contract-v1", QualifiedOutcomes = true });
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default), FrameworkSessionProviderTests.Observe, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.False(reply.ContinuationAvailable);
    }

    /// <summary>One external operation identity per invocation; oversized identities are refused before storage.</summary>
    [Fact]
    public async Task OperationIdentityIsSingleAndBounded()
    {
        List<ProviderObservation> seen = [];
        FrameworkTestAgent agent = new()
        {
            RunningWith = async (options, token) =>
            {
                FrameworkInvocation invocation = Required(options);
                await Assert.ThrowsAsync<ArgumentException>(() => invocation.ObserveNativeSessionAsync(new string('n', 513), token).AsTask());
                await invocation.ObserveOperationAcceptedAsync("first", token);
                await Assert.ThrowsAsync<InvalidOperationException>(() => invocation.ObserveOperationAcceptedAsync("second", token).AsTask());
                return Success();
            },
        };
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), await provider.OpenAsync(context, default),
            (observation, _) => { seen.Add(observation); return ValueTask.CompletedTask; }, default);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal([ProviderObservationKind.InvocationPrepared, ProviderObservationKind.OperationAccepted], seen.Select(item => item.Kind));
    }

    /// <summary>Another CLR type stored under the same options cannot impersonate the invocation context.</summary>
    [Fact]
    public void ForeignOptionsDoNotProvideInvocation()
    {
        AgentRunOptions options = new() { AdditionalProperties = new() { [typeof(FrameworkInvocation).FullName!] = "lookalike" } };
        Assert.False(FrameworkInvocation.TryGet(options, out _));
        Assert.False(FrameworkInvocation.TryGet(null, out _));
    }
}
