namespace Monstertacle.Multisession.AgentFramework.Evidence;
/// <summary>Frozen revision and dependencies of one project-owned evidence item.</summary>
/// <param name="Id">Unique nonblank project identity.</param>
/// <param name="Revision">Nonblank revision of this definition and its validation policy.</param>
/// <param name="Kind">Responsibility declared by the project.</param>
/// <param name="Dependencies">IDs of required predecessor evidence.</param>
public sealed record EvidenceNode(string Id, string Revision, EvidenceKind Kind, IReadOnlyList<string> Dependencies);
