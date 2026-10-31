using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Immutable reserved input and separately acknowledged native evidence share one persisted envelope.
// The native session observation is request evidence only and never replaces the stable binding reference.
internal sealed record StoredDispatch(ReservedDispatch Snapshot, string? SessionReference = null, string? OperationReference = null)
{
    public bool InvocationPrepared { get; init; }
    public string? NativeSessionReference { get; init; }
}
