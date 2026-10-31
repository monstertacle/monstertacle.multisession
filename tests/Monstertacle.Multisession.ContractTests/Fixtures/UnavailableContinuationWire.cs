using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.InteractionFixtureData;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class UnavailableContinuationWire : IDisposable
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal UnavailableContinuationWire() => Handler = new(SendAsync);
    internal ScriptedHttpHandler Handler { get; }
    internal Task Entered => _entered.Task;
    internal int Calls => Handler.Calls;
    internal void Release() => _release.TrySetResult();
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/interactions", request.RequestUri?.AbsolutePath);
        using JsonDocument input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        string? text = input.RootElement.GetProperty("input").GetString();
        JsonNode result = JsonNode.Parse(Json("root_completed"))!;
        if (text == "first")
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(token);
            result["status"] = "failed";
            result["steps"] = new JsonArray();
        }
        else
        {
            Assert.Equal("independent", text);
            result["id"] = "independent-root";
        }
        return ScriptedHttpHandler.Json(result.ToJsonString());
    }
    public void Dispose()
    {
        Release();
        Handler.Dispose();
    }
}
