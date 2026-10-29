using System.Text.Json;
using System.Text.Json.Nodes;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.OpenAI;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.OpenAiFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Opaque native context maintenance triggers no library context operation or transcript replay.</summary>
public sealed class NativeContextTests
{
    /// <summary>A passive server compaction item does not replace text, change binding or cause additional requests.</summary>
    [Fact]
    public async Task PreservesNativeContextWithoutOwnCompactionOrReplay()
    {
        int sends = 0;
        using ScriptedHttpHandler handler = new(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/responses", request.RequestUri?.AbsolutePath);
            using JsonDocument input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(sends == 0 ? "first new input" : "second new input", input.RootElement.GetProperty("input").GetString());
            Assert.False(input.RootElement.TryGetProperty("context_management", out _));
            Assert.False(input.RootElement.TryGetProperty("tools", out _));
            JsonNode response = JsonNode.Parse(Json("completed"))!;
            if (sends++ == 0)
            {
                ((JsonArray)response["output"]!).Insert(0, new JsonObject { ["type"] = "compaction", ["id"] = "cmp_fixture", ["encrypted_content"] = "opaque-native-state" });
            }
            return ScriptedHttpHandler.Json(response.ToJsonString());
        });
        using OpenAiSessionProvider provider = new(new() { CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply first = await provider.SendAsync(Call("first new input"), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(RequestState.Succeeded, first.Outcome);
        Assert.Equal("fixture output", first.Output);
        Assert.Equal(Binding(), first.Binding);
        ProviderReply second = await provider.SendAsync(Call("second new input"), first.Binding!, (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(RequestState.Succeeded, second.Outcome);
        Assert.Equal(Binding(), second.Binding);
        Assert.Equal("fixture output", first.Output);
        Assert.Equal(2, sends);
        Assert.Equal(2, handler.Calls);
    }
}
