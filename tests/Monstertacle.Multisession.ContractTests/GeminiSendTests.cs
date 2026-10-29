using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Gemini;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.GeminiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Distinguishes the first native identity, confirmed head and unconfirmed operation.</summary>
public sealed class GeminiSendTests
{
    private static readonly string[] _inputs = ["first input", "second input", "third input"];
    private static readonly string[] _fixtures = ["root_completed", "head_completed", "new_completed"];
    private static readonly string?[] _previous = [null, "interaction_root", "interaction_head"];

    /// <summary>Only the last confirmed head continues the native chain; prior transcript is never replayed.</summary>
    [Fact]
    public async Task SendsThreeNewInputsWithStableRootAndAdvancingHead()
    {
        int sends = 0;
        using ScriptedHttpHandler handler = new(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/interactions", request.RequestUri?.AbsolutePath);
            Assert.Equal($"fixture-key-{sends + 1}", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            Assert.Null(request.Headers.Authorization);
            using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonElement body = document.RootElement;
            Assert.Equal(_inputs[sends], body.GetProperty("input").GetString());
            Assert.Equal("opaque-deployment", body.GetProperty("model").GetString());
            Assert.Equal("fixed instruction", body.GetProperty("system_instruction").GetString());
            Assert.True(body.GetProperty("store").GetBoolean());
            Assert.True(body.GetProperty("background").GetBoolean());
            Assert.False(body.TryGetProperty("tools", out _));
            if (_previous[sends] is null) { Assert.False(body.TryGetProperty("previous_interaction_id", out _)); }
            else { Assert.Equal(_previous[sends], body.GetProperty("previous_interaction_id").GetString()); }
            return ScriptedHttpHandler.Json(Json(_fixtures[sends++]));
        });
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        List<ProviderObservation> observations = [];
        ValueTask Observe(ProviderObservation item, CancellationToken token) { observations.Add(item); return ValueTask.CompletedTask; }
        ProviderBinding binding = Lazy();
        foreach (string input in _inputs)
        {
            ProviderReply reply = await provider.SendAsync(Call(input), binding, Observe, CancellationToken.None);
            Assert.Equal(RequestState.Succeeded, reply.Outcome);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
            Assert.True(reply.OutputComplete);
            Assert.Equal("interaction_root", reply.Binding?.SessionReference);
            binding = reply.Binding!;
        }
        Assert.Equal("interaction_next", binding.ContinuationState);
        Assert.Equal(3, sends);
        Assert.Equal(4, observations.Count);
        Assert.Equal(new ProviderObservation(ProviderObservationKind.SessionEstablished, "interaction_root"), observations[0]);
        Assert.Equal(new ProviderObservation(ProviderObservationKind.OperationAccepted, "interaction_root"), observations[1]);
        Assert.Equal(new ProviderObservation(ProviderObservationKind.OperationAccepted, "interaction_head"), observations[2]);
        Assert.Equal(new ProviderObservation(ProviderObservationKind.OperationAccepted, "interaction_next"), observations[3]);
    }

    /// <summary>A new operation remains unconfirmed until persistence acknowledges it, before any GET or new head publication.</summary>
    [Fact]
    public async Task AwaitsOperationPersistenceBeforePolling()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(ScriptedHttpHandler.Json(Json("new_in_progress"))); }
            Assert.True(acknowledge.Task.IsCompletedSuccessfully);
            Assert.Equal("/v1/interactions/interaction_next", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(Json("new_completed")));
        });
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        Task<ProviderReply> pending = provider.SendAsync(Call(), Binding(), async (item, token) =>
        {
            Assert.Equal(new ProviderObservation(ProviderObservationKind.OperationAccepted, "interaction_next"), item);
            entered.TrySetResult();
            await acknowledge.Task.WaitAsync(token);
        }, CancellationToken.None).AsTask();
        await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(entered.Task.IsCompletedSuccessfully);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, handler.Calls);
        acknowledge.TrySetResult();
        ProviderReply reply = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("interaction_root", reply.Binding?.SessionReference);
        Assert.Equal("interaction_next", reply.Binding?.ContinuationState);
        Assert.Equal(2, handler.Calls);
    }

    /// <summary>Either first-identity or operation acknowledgement failure prevents success and further native reads.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StopsAfterFailedFirstIdentityOrOperationAcknowledgement(bool firstIdentity)
    {
        int acknowledgements = 0;
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("root_completed"))));
        using GeminiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Lazy(), (item, token) =>
        {
            acknowledgements++;
            if (firstIdentity || item.Kind == ProviderObservationKind.OperationAccepted) { throw new IOException("private persistence details"); }
            return ValueTask.CompletedTask;
        }, CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.Equal(ErrorCode.StorageUnavailable, reply.Error?.Code);
        Assert.Null(reply.Binding);
        Assert.Empty(reply.Output);
        Assert.Equal(firstIdentity ? 1 : 2, acknowledgements);
        Assert.Equal(1, handler.Calls);
    }
}
