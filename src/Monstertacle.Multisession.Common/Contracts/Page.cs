namespace Monstertacle.Multisession.Contracts;

/// <summary>A bounded result page with an opaque continuation cursor.</summary>
/// <typeparam name="T">Snapshot type.</typeparam>
/// <param name="Items">Snapshots in stable order.</param>
/// <param name="NextCursor">Opaque cursor, or null at the end.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
