namespace Monstertacle.Multisession.Contracts;

/// <summary>Explicit host retention; actual deletion additionally requires a conflict-free closed session.</summary>
/// <param name="RetainUntilExplicitDeletion">Whether no timed deletion is authorized.</param>
/// <param name="Duration">Positive finite duration when timed retention is selected.</param>
public sealed record RetentionPolicy(bool RetainUntilExplicitDeletion, TimeSpan? Duration);
