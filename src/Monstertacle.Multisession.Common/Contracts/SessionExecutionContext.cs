namespace Monstertacle.Multisession.Contracts;

/// <summary>A host-owned workspace snapshot; no workspace is inferred from prompts.</summary>
/// <param name="WorkspaceDirectory">Optional absolute directory required by workspace-based backends.</param>
/// <param name="WorkspaceKey">Canonical host key used for write serialization.</param>
public sealed record SessionExecutionContext(string? WorkspaceDirectory, string? WorkspaceKey);
