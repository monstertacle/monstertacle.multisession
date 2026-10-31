namespace Monstertacle.Multisession.AgentFramework.Evidence;
/// <summary>Project-declared evidence responsibility, with no project-specific validation rules.</summary>
public enum EvidenceKind
{
    /// <summary>Frozen source input, not a generated judgment.</summary>
    Source,
    /// <summary>Frozen governing rule input.</summary>
    Rule,
    /// <summary>Mechanically generated binding.</summary>
    Binding,
    /// <summary>Fresh scoped substantive judgment.</summary>
    Verdict,
    /// <summary>Authorized decision about a changed governing rule.</summary>
    RuleDecision,
    /// <summary>Output derived from accepted dependencies.</summary>
    Artifact,
}
