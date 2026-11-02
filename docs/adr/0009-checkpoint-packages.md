# ADR 0009: Checkpoint contract package and delivery checkpoint package

Status: accepted (feature 013); supersedes the package-name decision of ADR 0008. ADR 0007 (format and primitives) stands.
Context: owner request of 2026-11-02 for two packages, `Monstertacle.Multisession.Checkpoints` with base interfaces and
`Monstertacle.Multisession.Checkpoints.Delivery` with the concrete implementation, designed under SOLID.

## Decision

- `Monstertacle.Multisession.Checkpoints` contains only `ICheckpoint<TSnapshot> : IDisposable` with
  `Task<TSnapshot> ReadAsync(CancellationToken)`: a typed read of the domain's own snapshot and the ownership lifetime. It
  references only the runtime and promises nothing about persistence, leases, atomicity or delivery.
- `Monstertacle.Multisession.Checkpoints.Delivery` references it and contains every delivery type unchanged, in the namespace
  `Monstertacle.Multisession.Checkpoints.Delivery`. It adds `IDeliveryCheckpoint : ICheckpoint<CheckpointState>` with the
  existing stage, sending, preparation-failure, uncertain and accept operations; `DeliveryCheckpoint` implements it.
- Opening stays the concrete static `OpenAsync` and `OpenExistingAsync`. There is no generic opener or factory interface, because
  no caller is generic over storage.
- Grant helpers keep the concrete `DeliveryCheckpoint`, because a grant and its state change are one private transaction. Ordinary
  delivery code receives the `IDeliveryCheckpoint` view of the same object; one owner disposes it.
- The on-disk format of ADR 0007 is unchanged; no persisted value depends on a CLR name. There is no alias package, type
  forwarder or old-namespace facade, and 0.2.0-alpha.7 is not reissued.

## Reasons

Two opposed architecture reviews in two rounds. The first round put the delivery phase machine, `MarkSendingAsync` and grant
refusal reasons into the base to keep the host diff small; that was rejected, because domain states and grants belong to the
delivery domain and a "general" base holding them is a delivery interface under another name. The second round agreed on a
typed read and lifetime contract plus an inherited delivery port and on omitting the opener.

## Alternatives

- A base custody interface with stage, sending, uncertain and accept: the delivery state machine in a general package.
- Untyped `object` state or phase strings in the base: loses type safety and leaks persisted tokens into a contract.
- A second projection (`IStoredBytes`) of position and payload: no independent caller, and a second representation to keep consistent.
- A generic opener `ICheckpointOpener<TOptions, TCheckpoint>`: no caller needs it.
- Keeping `DeliveryCheckpoints` (ADR 0008): ADR 0008 rejected `Checkpoints` as a name that reads as session checkpoints; with an
  actual contract package and the explicit `Delivery` qualifier on the implementation, the owner's naming is unambiguous.

## Consequences

Hosts replace the package reference and namespace and regenerate lock files; checkpoint files stay valid. The package set
grows from eight to nine packages, and every package moves to a new prerelease. The generic contract alone does not make a
listener backend-independent: grants and composition still use the concrete delivery store.
