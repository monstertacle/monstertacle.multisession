using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.OpenAI;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.OpenAiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Checks one native POST per new input, durable operation acknowledgement and native polling.</summary>
public sealed class OpenAiSendTests
{
    private static readonly string[] _expectedInputs = ["first new input", "second new input"];
    /// <summary>Two sends retain conversation/model/settings and never replay preceding messages.</summary>
    [Fact]
    public async Task SendsOnlyNewInputWithStableConversationAndFreshCredential()
    {
        List<string> inputs = [];
        FixtureCredentials credentials = new();
        using ScriptedHttpHandler handler = new(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/responses", request.RequestUri?.AbsolutePath);
            Assert.Equal($"fixture-key-{inputs.Count + 1}", request.Headers.Authorization?.Parameter);
            using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonElement body = document.RootElement;
            Assert.Equal("opaque-model", body.GetProperty("model").GetString());
            Assert.Equal("conv_fixture", body.GetProperty("conversation").GetString());
            Assert.Equal("fixed instruction", body.GetProperty("instructions").GetString());
            Assert.True(body.GetProperty("background").GetBoolean());
            Assert.True(body.GetProperty("store").GetBoolean());
            Assert.False(body.TryGetProperty("previous_response_id", out _));
            Assert.False(body.TryGetProperty("tools", out _));
            inputs.Add(body.GetProperty("input").GetString()!);
            return ScriptedHttpHandler.Json(Json("completed"));
        });
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, credentials, handler);
        List<ProviderObservation> observations = [];
        ValueTask Observe(ProviderObservation item, CancellationToken token) { observations.Add(item); return ValueTask.CompletedTask; }
        ProviderReply first = await provider.SendAsync(Call("first new input"), Binding(), Observe, CancellationToken.None);
        ProviderReply second = await provider.SendAsync(Call("second new input"), first.Binding!, Observe, CancellationToken.None);
        Assert.Equal(_expectedInputs, inputs);
        Assert.All(new[] { first, second }, reply =>
        {
            Assert.Equal(RequestState.Succeeded, reply.Outcome);
            Assert.Equal(ExecutionCertainty.ConfirmedEnded, reply.Certainty);
            Assert.Equal("fixture output", reply.Output);
            Assert.True(reply.OutputComplete);
            Assert.Equal(Binding(), reply.Binding);
        });
        Assert.Equal(2, observations.Count);
        Assert.All(observations, item => Assert.Equal(new ProviderObservation(ProviderObservationKind.OperationAccepted, "resp_fixture"), item));
        Assert.Equal(2, credentials.Calls);
        Assert.Equal(2, handler.Calls);
    }

    /// <summary>Polling and success wait until the received native operation ID is durably acknowledged.</summary>
    [Fact]
    public async Task AwaitsOperationAcknowledgementBeforePolling()
    {
        TaskCompletionSource observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(ScriptedHttpHandler.Json(Json("queued"))); }
            Assert.True(acknowledge.Task.IsCompletedSuccessfully);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/responses/resp_fixture", request.RequestUri?.AbsolutePath);
            return Task.FromResult(ScriptedHttpHandler.Json(Json("completed")));
        });
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        Task<ProviderReply> pending = provider.SendAsync(Call(), Binding(), async (item, token) =>
        {
            Assert.Equal("resp_fixture", item.Reference);
            observed.TrySetResult();
            await acknowledge.Task.WaitAsync(token);
        }, CancellationToken.None).AsTask();
        await Task.WhenAny(observed.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(observed.Task.IsCompletedSuccessfully);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, handler.Calls);
        acknowledge.TrySetResult();
        ProviderReply reply = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.Equal(2, handler.Calls);
    }

    /// <summary>A failed acknowledgement prevents both polling and success, without resending the POST.</summary>
    [Fact]
    public async Task StopsAfterFailedOperationAcknowledgement()
    {
        using ScriptedHttpHandler handler = new((request, token) => Task.FromResult(ScriptedHttpHandler.Json(Json("queued"))));
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => throw new IOException("private callback details"), CancellationToken.None);
        Assert.Equal(ExecutionCertainty.Unknown, reply.Certainty);
        Assert.NotEqual(RequestState.Succeeded, reply.Outcome);
        Assert.Empty(reply.Output);
        Assert.False(reply.OutputComplete);
        Assert.Equal(ErrorCode.StorageUnavailable, reply.Error?.Code);
        Assert.Equal(1, handler.Calls);
    }
}
