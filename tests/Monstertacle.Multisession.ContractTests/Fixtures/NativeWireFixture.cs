using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

// Independently instantiated synthetic resources for each native adapter, never a live server.
internal sealed class NativeWireFixture : IDisposable
{
    private readonly bool _conversation;
    private readonly Task _release;
    private readonly ConcurrentDictionary<string, string> _completed = new(StringComparer.Ordinal);
    private int _next;
    private string? _confirmedHead;

    internal NativeWireFixture(bool conversation, Task release)
    {
        _conversation = conversation;
        _release = release;
        Handler = new(SendAsync);
    }
    internal ScriptedHttpHandler Handler { get; }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ConcurrentQueue<string> Inputs { get; } = new();
    internal ConcurrentQueue<string> Paths { get; } = new();

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        Paths.Enqueue($"{request.Method} {path}");
        if (_conversation && request.Method == HttpMethod.Post && path == "/v1/conversations")
        {
            return ScriptedHttpHandler.Json("""{"object":"conversation","id":"conv_runtime"}""");
        }
        string prefix = _conversation ? "/v1/responses" : "/v1/interactions";
        if (request.Method == HttpMethod.Post && path == prefix)
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string input = body.RootElement.GetProperty("input").GetString()!;
            string? previous = body.RootElement.TryGetProperty("previous_interaction_id", out JsonElement prior) ? prior.GetString() : null;
            if (!_conversation && previous != _confirmedHead) { return ScriptedHttpHandler.Json("{}", HttpStatusCode.BadRequest); }
            Inputs.Enqueue(input);
            int ordinal = Interlocked.Increment(ref _next);
            string id = (_conversation ? "resp_runtime_" : "interaction_runtime_") + ordinal;
            string final = NativeWireReply.Create(_conversation, id, input, previous, pending: false, passiveContext: ordinal == 1);
            _completed[id] = final;
            Entered.TrySetResult();
            if (ordinal == 1) { return ScriptedHttpHandler.Json(NativeWireReply.Create(_conversation, id, input, previous, pending: true, passiveContext: false)); }
            if (!_conversation) { _confirmedHead = id; }
            return ScriptedHttpHandler.Json(final);
        }
        if (request.Method == HttpMethod.Get && path.StartsWith(prefix + "/", StringComparison.Ordinal))
        {
            await _release.WaitAsync(cancellationToken);
            string id = path[(prefix.Length + 1)..];
            if (!_completed.TryGetValue(id, out string? response)) { return ScriptedHttpHandler.Json("{}", HttpStatusCode.NotFound); }
            if (!_conversation) { _confirmedHead = id; }
            return ScriptedHttpHandler.Json(response);
        }
        return ScriptedHttpHandler.Json("{}", HttpStatusCode.NotFound);
    }
    public void Dispose() => Handler.Dispose();
}
