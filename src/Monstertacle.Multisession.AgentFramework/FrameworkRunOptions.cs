using Microsoft.Agents.AI;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>
/// Run options that carry the SDK invocation context as a typed member, not as an additional property: a chat-client agent copies
/// additional properties into the chat options it sends, so the context would otherwise reach the chat client and its wire.
/// </summary>
internal sealed class FrameworkRunOptions : AgentRunOptions
{
    internal FrameworkRunOptions(FrameworkInvocation invocation) => Invocation = invocation;

    private FrameworkRunOptions(FrameworkRunOptions source) : base(source) => Invocation = source.Invocation;

    internal FrameworkInvocation Invocation { get; }

    /// <summary>A clone keeps the same invocation, so evidence reported through it still belongs to this dispatch.</summary>
    public override AgentRunOptions Clone() => new FrameworkRunOptions(this);
}
