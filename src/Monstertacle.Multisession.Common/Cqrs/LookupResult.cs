namespace Monstertacle.Multisession.Cqrs;

/// <summary>A non-null result envelope preserving lookup absence.</summary>
/// <typeparam name="T">The reference snapshot type.</typeparam>
/// <param name="Value">The snapshot, or null when absent.</param>
public sealed record LookupResult<T>(T? Value) where T : class;
