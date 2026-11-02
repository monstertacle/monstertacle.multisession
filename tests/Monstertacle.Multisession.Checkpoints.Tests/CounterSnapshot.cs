namespace Monstertacle.Multisession.Checkpoints.Tests;

/// <summary>A domain snapshot unrelated to delivery: the contract imposes no members on it.</summary>
/// <param name="Position">Last processed position.</param>
/// <param name="Label">Domain label.</param>
public sealed record CounterSnapshot(long Position, string Label);
