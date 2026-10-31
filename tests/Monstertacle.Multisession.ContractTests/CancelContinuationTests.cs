using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Native end evidence cannot publish an obsolete operation-dependent continuation.</summary>
public sealed class CancelContinuationTests
{
    /// <summary>Cancellation verifies the accepted operation through the declared recovery port before adopting its head.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("failure")]
    public async Task VerifiesOperationDependentContinuation(string mode)
    {
        TaskCompletionSource accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ProviderReply> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider inner = new()
        {
            OpeningBinding = new("controlled", 1, "native", "old-head"),
            Capabilities = new(true, true, true, true, false) { RequiresOperationAwareRecovery = true },
            Sending = async (call, binding, observe, token) =>
            {
                await observe(new(ProviderObservationKind.OperationAccepted, "operation"), token);
                accepted.TrySetResult();
                return await finish.Task;
            },
            Cancelling = (context, operation, token) => ValueTask.FromResult(new ProviderReply(RequestState.Cancelled,
                ExecutionCertainty.ConfirmedEnded, string.Empty, false, null, new(ErrorCode.Cancelled))),
        };
        OperationAwareProvider aware = new()
        {
            Inner = inner,
            Recovering = (binding, operation, token) =>
            {
                Assert.Equal("operation", operation);
                Assert.Equal("old-head", binding.ContinuationState);
                if (mode == "failure") { throw new IOException("private-reopen-marker"); }
                return ValueTask.FromResult(binding with { SessionReference = mode == "foreign" ? "foreign" : "native", ContinuationState = "verified-head" });
            },
        };
        ISessionProvider provider = mode == "missing" ? inner : aware;
        InMemorySessionStore store = new();
        RuntimeOptions options = RuntimeTestComposition.Options();
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        SessionCatalog catalog = new(store, registrations, [new("profile", "connection", "model", ExecutionKind.ModelResponse, new("revision", new Dictionary<string, string>()))]);
        SessionSnapshot session = await catalog.CreateSessionAsync(Guid.NewGuid(), "profile", new(null, null), CancellationToken.None);
        RequestAdmission requests = new(store, options);
        RequestReceipt receipt = await requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "new input", TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
        Task<bool> pending = new RequestDispatcher(store, registrations, options).TryDispatchAsync(CancellationToken.None).AsTask();
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await requests.CancelRequestAsync(receipt.RequestId, CancellationToken.None);
            if (mode == "valid")
            {
                Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
                Assert.Empty(store.Reservations);
                Assert.Equal("verified-head", store.FindSession(session.SessionId)?.Binding?.ContinuationState);
            }
            else
            {
                SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
                Assert.DoesNotContain("private-reopen-marker", failure.ToString(), StringComparison.Ordinal);
                Assert.Single(store.Reservations);
                Assert.Equal("old-head", store.FindSession(session.SessionId)?.Binding?.ContinuationState);
            }
            Assert.Equal(mode == "missing" ? 0 : 1, aware.RecoveryCalls);
            Assert.Equal(0, aware.LegacyReopenCalls);
        }
        finally { finish.TrySetResult(new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "late", true, session.Binding, null)); }
    }
}
