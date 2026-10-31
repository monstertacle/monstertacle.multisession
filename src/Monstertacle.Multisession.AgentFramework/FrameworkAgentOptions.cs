using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>Explicit host contract for framework-managed sessions, not inferred from agent names.</summary>
public sealed record FrameworkAgentOptions
{
    /// <summary>Gets the stable registration/serializer contract identity chosen by the host.</summary>
    public required string AdapterId { get; init; }
    /// <summary>Gets the explicitly supported execution semantics.</summary>
    public ExecutionKind ExecutionKind { get; init; } = ExecutionKind.AgentTask;
    /// <summary>Gets whether the host requires workspace binding.</summary>
    public bool RequiresWorkspace { get; init; }
    /// <summary>Gets the maximum UTF8 serialized continuation size, including identity envelope.</summary>
    public int MaximumStateBytes { get; init; } = 1024 * 1024;
    /// <summary>Gets the finite creation/restoration/serialization operation deadline.</summary>
    public TimeSpan SessionOperationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
