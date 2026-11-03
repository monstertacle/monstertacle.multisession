using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.AgentFramework;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>
/// The SDK's invocation context reaches a real ChatClientAgent only as run options: it never appears in the chat options or messages
/// a chat client receives, nor in their wire serialization.
/// </summary>
public sealed class FrameworkChatClientIsolationTests
{
    private static readonly string _invocationKey = typeof(FrameworkInvocation).FullName!;

    /// <summary>A clone of the run options keeps the same invocation; a context placed in additional properties is not accepted.</summary>
    [Fact]
    public async Task OnlyTheAdapterRunOptionsAndTheirClonesCarryTheInvocation()
    {
        FrameworkInvocation? original = null, cloned = null;
        bool smuggled = true;
        FrameworkTestAgent agent = new()
        {
            RunningWith = (options, token) =>
            {
                Assert.True(FrameworkInvocation.TryGet(options, out original));
                Assert.True(FrameworkInvocation.TryGet(options!.Clone(), out cloned));
                AgentRunOptions copied = new() { AdditionalProperties = new() { [_invocationKey] = original } };
                smuggled = FrameworkInvocation.TryGet(copied, out _);
                return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "done")) { FinishReason = ChatFinishReason.Stop });
            },
        };
        FrameworkSessionProvider provider = FrameworkSessionProviderTests.Provider(agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderBinding opened = await provider.OpenAsync(context, default);

        ProviderReply reply = await provider.SendAsync(FrameworkSessionProviderTests.Call(context), opened, (_, _) => ValueTask.CompletedTask, default);

        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.NotNull(original);
        Assert.Same(original, cloned);
        Assert.False(smuggled);
    }

    /// <summary>A real ChatClientAgent over a recording chat client succeeds and forwards no part of the invocation context.</summary>
    [Fact]
    public async Task InvocationContextNeverReachesChatOptionsOrTheWire()
    {
        RecordingChatClient chat = new();
        ChatClientAgent agent = new(chat);
        FrameworkSessionProvider provider = new(new() { AdapterId = "chat-client-v1" }, _ => agent);
        ProviderSessionContext context = FrameworkSessionProviderTests.Context();
        ProviderCall call = FrameworkSessionProviderTests.Call(context, "question for the model");
        ProviderBinding opened = await provider.OpenAsync(context, default);

        ProviderReply reply = await provider.SendAsync(call, opened, (_, _) => ValueTask.CompletedTask, default);

        Assert.Equal(RequestState.Succeeded, reply.Outcome);
        Assert.Equal("recorded answer", reply.Output);
        (IReadOnlyList<ChatMessage> messages, ChatOptions? options) = Assert.Single(chat.Requests);
        Assert.Contains(messages, message => message.Text == "question for the model");
        Assert.False(options?.AdditionalProperties?.ContainsKey(_invocationKey) ?? false, "invocation key in chat options");
        Assert.DoesNotContain(options?.AdditionalProperties?.Values ?? [], value => value is FrameworkInvocation);
        Assert.DoesNotContain(messages.SelectMany(message => message.AdditionalProperties?.Values ?? []), value => value is FrameworkInvocation);
        string wire = JsonSerializer.Serialize(new { messages, options }, AIJsonUtilities.DefaultOptions);
        Assert.DoesNotContain(_invocationKey, wire, StringComparison.Ordinal);
        Assert.DoesNotContain(call.DispatchId.ToString(), wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(call.DispatchId.ToString("N"), wire, StringComparison.OrdinalIgnoreCase);
    }
}
