using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>
/// Pins the evidence boundary a host relies on: an unknown end never becomes "not started", and a reply's output is cleared on the
/// uncertain, rejected and cancelled paths, so partial bytes are never published as a result. A host that needs those bytes as
/// evidence keeps its own bounded copy; this library adds no partial-output record.
/// </summary>
public sealed class PartialEvidencePinningTests
{
    /// <summary>All state pairs: evidence of an unknown end never transitions to evidence of no start.</summary>
    [Fact]
    public void UnknownNeverBecomesNotStarted()
    {
        RequestState[] states = Enum.GetValues<RequestState>();
        foreach (RequestState current in states)
        {
            foreach (RequestState next in states)
            {
                Assert.False(RequestStateMachine.CanTransition(current, ExecutionCertainty.Unknown, next, ExecutionCertainty.NotStarted),
                    $"{current}/Unknown -> {next}/NotStarted");
            }
        }
    }

    /// <summary>A terminal reply whose binding identity changed is uncertain, and its output is cleared.</summary>
    [Fact]
    public void ClearsOutputOnTheUncertainPath()
    {
        ProviderBinding original = new("controlled", 1, "native-session", null);
        ProviderReply reply = new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "partial native text", true,
            original with { SessionReference = "other-session" }, null);

        ProviderReply normalized = DispatchReplyPolicy.Normalize(reply, original, null, 1024);

        Assert.Equal(RequestState.Interrupted, normalized.Outcome);
        Assert.Equal(ExecutionCertainty.Unknown, normalized.Certainty);
        Assert.Equal(string.Empty, normalized.Output);
        Assert.False(normalized.OutputComplete);
    }

    /// <summary>A success without complete output is rejected, and its partial output is cleared.</summary>
    [Fact]
    public void ClearsOutputOnTheRejectedPath()
    {
        ProviderReply reply = new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "partial native text", false, null, null);

        ProviderReply normalized = ProviderReplyPolicy.NormalizeOutput(reply, 1024);

        Assert.Equal(RequestState.Failed, normalized.Outcome);
        Assert.Equal(string.Empty, normalized.Output);
        Assert.False(normalized.OutputComplete);
    }

    /// <summary>
    /// A running request cancelled through the runtime and confirmed by the provider's cancel reply keeps no output, whether that
    /// reply carries complete or partial text: the termination step clears it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClearsOutputOnTheCancelledPath(bool outputComplete)
    {
        TaskCompletionSource accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native", null),
            Capabilities = new(true, true, false, true, false),
            Sending = async (call, binding, observe, token) =>
            {
                // The accepted operation lets the runtime confirm the end through the provider's cancel reply.
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                accepted.TrySetResult();
                return await finish.Task;
            },
            Cancelling = (context, operation, token) => ValueTask.FromResult(new ProviderReply(RequestState.Cancelled,
                ExecutionCertainty.ConfirmedEnded, "partial native text", outputComplete, null, new(ErrorCode.Cancelled))),
        };
        InMemorySessionStore store = new();
        RuntimeOptions options = RuntimeTestComposition.Options();
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        SessionCatalog catalog = new(store, registrations,
            [new("profile", "connection", "model", ExecutionKind.ModelResponse, new("revision", new Dictionary<string, string>()))]);
        SessionSnapshot session = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        RequestAdmission requests = new(store, options);
        RequestReceipt receipt = await requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromMinutes(1), null, false),
            CancellationToken.None);
        Task<bool> pending = new RequestDispatcher(store, registrations, options).TryDispatchAsync(CancellationToken.None).AsTask();
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            // The confirmed cancellation completes the dispatch.
            Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            RequestSnapshot cancelled = (await requests.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            Assert.Equal(RequestState.Cancelled, cancelled.State);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, cancelled.Certainty);
            Assert.Equal(string.Empty, cancelled.Result?.Output);
            Assert.False(cancelled.Result?.OutputComplete);
        }
        finally { finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, null, null)); }
    }
}
