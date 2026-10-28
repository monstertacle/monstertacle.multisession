using System.Collections.Frozen;

namespace Monstertacle.Multisession.Contracts;

/// <summary>Immutable session context supplied to a registered execution adapter.</summary>
/// <param name="SessionId">Local session identity.</param>
/// <param name="ModelId">Opaque model identifier, unchanged by the core.</param>
/// <param name="ExecutionKind">Requested agent or model semantics.</param>
/// <param name="ExecutionContext">Host-owned workspace binding.</param>
/// <param name="Settings">Non-secret immutable profile settings supplied by the host.</param>
public sealed record ProviderSessionContext(
    Guid SessionId,
    string ModelId,
    ExecutionKind ExecutionKind,
    SessionExecutionContext ExecutionContext,
    IReadOnlyDictionary<string, string> Settings)
{
    /// <summary>Gets the independently frozen, ordinal profile snapshot.</summary>
    public IReadOnlyDictionary<string, string> Settings { get; } = Settings.ToFrozenDictionary(StringComparer.Ordinal);
}
