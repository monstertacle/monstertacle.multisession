using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Immutable reserved input and separately acknowledged native evidence share one persisted envelope.
internal sealed record StoredDispatch(ReservedDispatch Snapshot, string? SessionReference = null, string? OperationReference = null);
