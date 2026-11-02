# Checkpoint contracts

Status: decided on 2026-11-02 after two opposed architecture reviews; this is the implementation contract.

## Base (`Monstertacle.Multisession.Checkpoints`, BCL only)

```csharp
namespace Monstertacle.Multisession.Checkpoints;

public interface ICheckpoint<TSnapshot> : IDisposable
{
    Task<TSnapshot> ReadAsync(CancellationToken cancellationToken);
}
```

The contract covers a typed read and the ownership lifetime, and nothing more. The domain implementation owns the snapshot's
meaning and validation.

The contract makes no claim about persistence, leases, atomicity or delivery for implementations in general. It imposes no
members or constraints on `TSnapshot`.

The base contains no opener, no factory, no exception type, no phase, no grant, no route and no `object` state.

## Delivery (`Monstertacle.Multisession.Checkpoints.Delivery`, references the base)

```csharp
namespace Monstertacle.Multisession.Checkpoints.Delivery;

public interface IDeliveryCheckpoint : ICheckpoint<CheckpointState>
{
    Task StageAsync(long position, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    Task MarkSendingAsync(CancellationToken cancellationToken);
    Task<bool> RecordPreparationFailureAsync(CancellationToken cancellationToken);
    Task<bool> MarkUncertainAsync(CancellationToken cancellationToken);
    Task AcceptAsync(ReadOnlyMemory<byte> expectedPayload, string acknowledgement, CancellationToken cancellationToken);
}

public sealed class DeliveryCheckpoint : IDeliveryCheckpoint
{
    public static Task<DeliveryCheckpoint> OpenAsync(DeliveryCheckpointOptions options, CancellationToken cancellationToken);
    public static Task<DeliveryCheckpoint> OpenExistingAsync(DeliveryCheckpointOptions options, CancellationToken cancellationToken);
    // existing members, semantics, refusals and cancellation unchanged
}
```

## Composition

- The composition root keeps the concrete `DeliveryCheckpoint` it opened. The grant helpers (`AttemptGrants`,
  `ResendAuthorizations`, `RouteMoves`) are constructed from that concrete object, because they share its private
  transaction.
- Ordinary reads, transitions and the lifetime use `IDeliveryCheckpoint` as a reference view of the same object.
- There is no storage-handle interface, no downcast, no second store or lease, and exactly one disposal owner.

## Not provided, deliberately

- No generic opener: no caller needs one.
- No alias package, type forwarder or old-namespace facade.
- No base exception or refusal vocabulary: refusals stay typed in Delivery.
