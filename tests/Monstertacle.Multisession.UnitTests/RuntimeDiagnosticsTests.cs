using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Runtime diagnostic events follow acknowledged durable writes.</summary>
public sealed class RuntimeDiagnosticsTests
{
    /// <summary>Real runtime hooks cannot lose a receipt or stored success when diagnostics fail.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeReportsOnlyAfterConfirmedWrites(bool throwing)
    {
        CaptureLogger logger = new() { Throw = throwing };
        using SessionDiagnostics diagnostics = new(logger);
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        await using SessionRuntime runtime = new(scenario.Store, new TestStateDirectoryLease(scenario.Options.StateDirectory),
            [new("connection", "controlled", 1, scenario.Provider)], [], diagnostics: diagnostics);
        await runtime.StartAsync(scenario.Options, CancellationToken.None);
        RequestReceipt receipt = await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(scenario.Session.SessionId, "SECRET-INPUT", TimeSpan.FromSeconds(5), null, false), CancellationToken.None);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        RequestSnapshot? request;
        do
        {
            request = await runtime.Requests.GetRequestAsync(receipt.RequestId, deadline.Token);
            if (request?.State == RequestState.Succeeded) { break; }
            await Task.Delay(10, deadline.Token);
        } while (true);
        await runtime.StopAsync(TimeSpan.FromSeconds(5), deadline.Token);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request.Certainty);
        if (throwing) { Assert.Equal(2, diagnostics.FailureCount); }
        else
        {
            Assert.Equal(2, logger.Events.Count);
            Assert.All(logger.Events, item => Assert.Equal(receipt.RequestId, item["request.id"]));
            Assert.Contains(logger.Events, item => Equals(item["request.state"], "Succeeded"));
        }
    }

    /// <summary>Unknown is logged only after its write is acknowledged; lost acknowledgements never produce a guessed result.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task StoreAcknowledgementControlsCompletionEvent(bool lostCompletion, bool lostUncertainty)
    {
        InMemorySessionStore store = new()
        {
            DispatchState = new()
            {
                Completed = lostCompletion ? () => throw new IOException("SECRET-ACK") : null,
                MarkingUncertain = lostUncertainty ? token => throw new IOException("SECRET-STORE") : null
            }
        };
        ControlledProvider? provider = lostCompletion ? null : new() { Sending = (call, binding, observe, token) => throw new IOException("SECRET-PROVIDER") };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider, store);
        await scenario.SubmitAsync();
        CaptureLogger logger = new();
        using SessionDiagnostics diagnostics = new(logger);
        RequestDispatcher dispatcher = new(store, [new("connection", "controlled", 1, scenario.Provider)], scenario.Options, diagnostics: diagnostics);
        await Assert.ThrowsAsync<SessionOperationException>(() => dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        if (lostCompletion || lostUncertainty) { Assert.Empty(logger.Events); }
        else
        {
            Dictionary<string, object?> entry = Assert.Single(logger.Events);
            Assert.Equal("Failed", entry["request.state"]);
            Assert.Equal("Unknown", entry["execution.certainty"]);
        }
    }

}
