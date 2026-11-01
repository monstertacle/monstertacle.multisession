# Delivery checkpoint (`Monstertacle.Multisession.Delivery`)

Durable custody of one delivery channel to a destination that is not idempotent. The package stores one opaque pending
item, its phase and attempt counter in SQLite under an exclusive lease, and records three kinds of host-authorized grants
atomically with their state change. It sends nothing, retries nothing and runs nothing in the background. It has no
dependency on the session packages, Microsoft Agent Framework or any host. Specification: `specs/009-delivery-checkpoint`,
decision: `docs/adr/0007-delivery-checkpoint-primitives.md`.

## Contract

```csharp
var options = new DeliveryCheckpointOptions(statePath, new DeliveryChannel(consumerId, sourceId, routeId));
using var checkpoint = await DeliveryCheckpoint.OpenAsync(options, ct);   // owner: creates, WAL, Sending -> Uncertain
var state = await checkpoint.ReadAsync(ct);
if (state.Pending is null) await checkpoint.StageAsync(state.Position + 1, canonicalBytes, ct);
await checkpoint.MarkSendingAsync(ct);                  // immediately before the external effect
// ... host performs the effect through its own adapter ...
await checkpoint.AcceptAsync(canonicalBytes, acknowledgement, ct);   // or MarkUncertainAsync when the outcome is unknown
```

| Operation | Allowed from | Effect |
| --- | --- | --- |
| `StageAsync(position, payload)` | nothing pending, `position == Position + 1` | Pending, attempts 0 |
| `MarkSendingAsync` | Pending | Sending, attempts + 1 |
| `RecordPreparationFailureAsync` | Pending | attempts + 1 (false otherwise) |
| `MarkUncertainAsync` | Sending | Uncertain (false otherwise) |
| `AcceptAsync(payload, ack)` | Sending with identical bytes | Accepted, position + 1, item cleared, ack kept |
| `AttemptGrants.RecordAsync` | expectation Pending, attempts above earlier grants | receipt only |
| `ResendAuthorizations.AuthorizeAsync` | expectation Uncertain, item never authorized | receipt + Sending, attempts + 1 |
| `RouteMoves.MoveAsync` | expectation Pending | receipt + route change |

`OpenExistingAsync` opens an existing checkpoint for inspection and grants without creating or recovering anything.
Grants take a `CheckpointExpectation` (route, position, digest of the stored bytes, phase, attempts), a request id for
idempotency, the host-attested item id and the host's canonical receipt text. A repeated request id returns the stored
receipt unchanged; different values for the same id are a `RequestConflict`. Each grant journal keeps at most 1024 receipts
and refuses when full. All refusals are `DeliveryRefusalException` with a typed `DeliveryRefusal` and change nothing.

Guarantee: at most one automatic Sending per staged item and at most one authorized resend per item. Accepted only means
the host reported a positive acknowledgement. There is no exactly-once delivery. Delivery uncertainty is independent of
session execution recovery in the other packages.

## Format and platforms

The on-disk format is the current format of existing checkpoints and is retained exactly: tables `checkpoint`,
`preparation_rearms`, `delivery_replays`, `delivery_rebinds`, their `CREATE TABLE` text, phase strings, `schema = 1` and
the sibling lease file `<state>.lock`. Anything else is refused; there is no converter or alternate reader. The owner open
keeps state owner-only: on Linux the directory is 0700 and the files 0600; on Windows the state directory gets a protected
access rule for the current user only. Both platforms are tested in native CI.

## Host integration

A host keeps its feed, payload contract and canonical serialization, readiness checks, configuration reload, operator
authorization, routing policy and external adapters. It maps its identities to `DeliveryChannel`, its key records to
`CheckpointExpectation`, serializes receipts itself and maps `DeliveryRefusal` values to its own operator codes.

## Provenance and license

The checkpoint format and the transaction rules were extracted from the owner's SharpClaw Listener (source commit
`2c4a8af153f866d09bfb564fa88d4ac2856b20ec`) on the owner's instruction and are published here under the repository MIT
license. This covers only the extracted package; it makes no statement about the license of the rest of SharpClaw. The
package contains no third-party code; Microsoft.Data.Sqlite is a NuGet dependency under its own license.
