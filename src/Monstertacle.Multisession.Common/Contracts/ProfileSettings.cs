namespace Monstertacle.Multisession.Contracts;

/// <summary>Host-owned non-secret settings to freeze when a session is created.</summary>
/// <param name="Revision">Opaque host configuration revision.</param>
/// <param name="Values">Allowed non-secret profile values.</param>
public sealed record ProfileSettings(string Revision, IReadOnlyDictionary<string, string> Values);
