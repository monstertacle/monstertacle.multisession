# Data model: Checkpoint base contracts and the delivery checkpoint package

The persisted model is unchanged from feature 009 (ADR 0007): one `checkpoint` row (schema 1) and three receipt journals
(`preparation_rearms`, `delivery_replays`, `delivery_rebinds`). See `src/.../CheckpointFormat.cs`. This feature only moves types.

## Assembly placement

| Type | Assembly after the split | Note |
| --- | --- | --- |
| Base read/lifetime contract (name pending) | `Monstertacle.Multisession.Checkpoints` | Generic over the snapshot; no delivery type |
| Delivery checkpoint port (name pending) | `Monstertacle.Multisession.Checkpoints.Delivery` | Extends the base contract over `CheckpointState` |
| `DeliveryCheckpoint` | Delivery | Implements the port |
| `DeliveryCheckpointOptions`, `DeliveryChannel`, `DeliveryLimits` | Delivery | Unchanged members |
| `CheckpointState`, `CheckpointExpectation`, `PendingItem`, `DeliveryPhase` | Delivery | Unchanged members |
| `DeliveryRefusal`, `DeliveryRefusalException` | Delivery | All 13 reasons; unchanged |
| Grant helpers and records | Delivery | Constructed from the concrete store |
| All internal storage types | Delivery | Unchanged |

Namespace for all Delivery types: `Monstertacle.Multisession.Checkpoints.Delivery`. Base namespace:
`Monstertacle.Multisession.Checkpoints`.

## State transitions

Unchanged; see `docs/delivery.md`, operation table. The base contract carries no transition.
