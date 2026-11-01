# Opaque delivery checkpoint and atomic grant primitives

Status: accepted for implementation (feature 009); host activation is a separate decision.

## Context

The SharpClaw Listener holds the durable custody of one delivery channel to a non-idempotent destination: a single-row SQLite
checkpoint (pending, sending, uncertain, accepted), an exclusive file lease and three operator grant journals (attempt rearm,
single resend, route move). The owner commissioned extracting reusable delivery capability into Monstertacle and requires
SharpClaw to be treated as one of many consumers. Two opposed reviews agreed that payload semantics, feeds, destinations,
readiness, configuration and authorization are host policy, and that a receipt and its state change must stay one transaction.

## Decision

`Monstertacle.Multisession.Delivery` owns only the checkpoint and three typed atomic primitives. The host supplies canonical
opaque payload bytes and canonical receipt text and authorizes every grant; the library enforces the exact expectation
(route, position, digest of stored bytes, phase, attempts), idempotency by request id, unique resend per item, capacity and the
CAS transition inside one library-owned transaction. No connection, callback or policy hook is exposed inside a transaction.
The current on-disk format is the package's canonical format: same tables, columns, DDL text, phase strings and lock file.
Stored data is never converted; no reader for any other format exists. Public names are host-neutral; private names follow the
format. Unix and Windows private access are implemented in the package.

## Alternatives

- Keep everything in the host (STATUSQUO): declined because the owner made the library a binding requirement; its safety
  conditions (B1–B6) are adopted as requirements.
- Full delivery module with source, destination interface and attempt runner (CHANGE): rejected; it would move external
  effects and orchestration into the SDK and create a second execution path.
- Store in the SDK, grants in the host over an exposed connection: rejected (atomicity and encapsulation, finding F2).
- New neutral schema with an importer: rejected; it is a migration of live custody and a second reader.

## Consequences

Hosts depend on one small package and keep their operator codes through an explicit mapping. The format carries host-shaped
column names privately. There is no exactly-once guarantee; Accepted means a positive adapter acknowledgement. Delivery
uncertainty stays separate from session execution recovery. The extracted code is the owner's and is published under MIT with
provenance recorded in feature 009; this does not relicense the rest of SharpClaw.
