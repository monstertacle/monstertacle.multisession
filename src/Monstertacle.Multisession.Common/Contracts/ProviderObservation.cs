namespace Monstertacle.Multisession.Contracts;

/// <summary>Nonterminal evidence whose callback must be awaited before proceeding.</summary>
/// <param name="Kind">The kind of evidence.</param>
/// <param name="Reference">Opaque session or operation identity; no prompt or credentials.</param>
public sealed record ProviderObservation(ProviderObservationKind Kind, string? Reference);
