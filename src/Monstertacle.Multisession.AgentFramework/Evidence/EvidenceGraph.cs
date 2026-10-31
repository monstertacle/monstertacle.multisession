using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Monstertacle.Multisession.AgentFramework.Evidence;

/// <summary>Immutable project dependency graph with transitive revision fingerprints.</summary>
public sealed class EvidenceGraph
{
    private readonly FrozenDictionary<string, string> _fingerprints;
    /// <summary>Freezes and validates definitions before any project callbacks.</summary>
    public EvidenceGraph(IReadOnlyList<EvidenceNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        Dictionary<string, EvidenceNode> definitions = [];
        foreach (EvidenceNode node in nodes)
        {
            if (node is null || string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Revision)
                || !Enum.IsDefined(node.Kind) || node.Dependencies is null || definitions.ContainsKey(node.Id))
            { throw new ArgumentException("Evidence definitions require unique identities, revisions and supported kinds.", nameof(nodes)); }
            string[] dependencies = [.. node.Dependencies];
            if (dependencies.Any(string.IsNullOrWhiteSpace) || dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Length
                || (IsInput(node) && dependencies.Length != 0))
            { throw new ArgumentException("Input nodes must be leaves; dependencies must be distinct identities.", nameof(nodes)); }
            definitions.Add(node.Id, node with { Dependencies = Array.AsReadOnly(dependencies) });
        }
        if (definitions.Values.Any(node => node.Dependencies.Any(id => !definitions.ContainsKey(id))))
        { throw new ArgumentException("Every dependency must have a definition.", nameof(nodes)); }
        List<EvidenceNode> ordered = [];
        Dictionary<string, string> fingerprints = [];
        List<EvidenceNode> remaining = [.. definitions.Values.OrderBy(node => node.Id, StringComparer.Ordinal)];
        while (remaining.Count > 0)
        {
            EvidenceNode next = remaining.FirstOrDefault(node => node.Dependencies.All(fingerprints.ContainsKey))
                ?? throw new ArgumentException("Evidence dependencies contain a cycle.", nameof(nodes));
            string[] revision = [next.Id, next.Kind.ToString(), next.Revision, .. next.Dependencies.Order(StringComparer.Ordinal).SelectMany(id => new[] { id, fingerprints[id] })];
            fingerprints.Add(next.Id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(revision)))));
            ordered.Add(next);
            remaining.Remove(next);
        }
        Nodes = ordered.AsReadOnly();
        _fingerprints = fingerprints.ToFrozenDictionary(StringComparer.Ordinal);
    }
    /// <summary>Gets immutable nodes in deterministic dependency order.</summary>
    public IReadOnlyList<EvidenceNode> Nodes { get; }
    /// <summary>Gets the exact transitive identity/revision fingerprint.</summary>
    public string Fingerprint(string id) => _fingerprints[id];
    internal static bool IsInput(EvidenceNode node) => node.Kind is EvidenceKind.Source or EvidenceKind.Rule;
}
