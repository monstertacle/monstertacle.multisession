namespace Monstertacle.Multisession.Contracts;

/// <summary>The execution semantics explicitly requested by a host.</summary>
public enum ExecutionKind
{
    /// <summary>A model response without an agent-runtime promise.</summary>
    ModelResponse,
    /// <summary>A task executed by an external agent runtime.</summary>
    AgentTask,
}
