using Microsoft.Extensions.AI;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

/// <summary>A chat client that records every request it would put on the wire and answers with one fixed assistant message.</summary>
internal sealed class RecordingChatClient : IChatClient
{
    internal List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(([.. messages], options));
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "recorded answer")) { FinishReason = ChatFinishReason.Stop });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates()) { yield return update; }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
