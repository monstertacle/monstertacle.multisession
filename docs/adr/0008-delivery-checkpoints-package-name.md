# ADR 0008: Package name DeliveryCheckpoints

Status: accepted
Context: owner request for a more precise name than `Delivery` (2026-11-01); spec 010.

## Decision

The delivery checkpoint package is `Monstertacle.Multisession.DeliveryCheckpoints` (package id, project, assembly, namespace) from
0.2.0-alpha.7. Public type names (`DeliveryCheckpoint`, `DeliveryChannel`, `DeliveryPhase`, `DeliveryRefusal`, `CheckpointState`, the
grant types) and the on-disk format of ADR 0007 are unchanged. The `outbox` package tag is removed. There is no alias package or type
forwarder; 0.2.0-alpha.6 stays as published locally and is not reissued.

## Reasons

Two opposed reviews on the same frozen evidence reached the same recommendation. "Delivery checkpoint" is the term the spec, ADR 0007,
the documentation and the first host already use; the bare `Delivery` id promised the send/runner module ADR 0007 rejected. The plural
avoids a namespace equal to its main type.

## Alternatives

- Keep `Delivery`: zero churn, but misleading on its own.
- `Outbox`: implies the transactional outbox pattern with a dispatcher.
- `Checkpoints`: reads as session or workflow checkpoints in a multisession package family.
- `EffectCustody` / `ExternalEffectCheckpoint`: names the guarantee best but forces renaming all `Delivery*` types while send-shaped
  members and the fixed on-disk phase names remain, leaving a mixed surface.

## Consequences

Hosts change the package reference, namespaces and lock files. "Delivery" stays as a qualifier and "checkpoint" keeps other meanings
elsewhere; the package README and docs/delivery.md state that the package sends nothing.
