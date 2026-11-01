# Plan: Monstertacle.Multisession.Delivery

## Technical context

.NET SDK 10.0.403, C# 14, net10.0 single target (Linux and Windows, x64 CI runners `ubuntu-24.04`, `windows-latest`),
Microsoft.Data.Sqlite 10.0.14 (central pin). xUnit test project `tests/Monstertacle.Multisession.Delivery.Tests` (net10.0).

## Constitution check

- I: async APIs with CancellationToken; no blocking waits; SQLite busy timeout is a bounded option (default 30 s, ≤ 60 s).
- II: deep dive in research.md; small types: `DeliveryCheckpoint` (custody/transitions), `AttemptGrants`, `ResendAuthorizations`,
  `RouteMoves` (one grant kind each), internal `CheckpointFile` (lease, privacy, connection), `CheckpointFormat` (DDL and phase
  text), `GrantJournal` (shared transaction mechanics for the three journals). No host types, no adapters, no feed, no callbacks.
- III: Codemetr full check after each change; no baseline.
- IV: TDD per task with recorded Red/Green; ≥ 87 percent per platform including the new assembly.
- V: parameterized SQL only; payloads, receipts and acknowledgements are never logged.
- Platform: Unix and Windows branches inside one assembly behind `OperatingSystem` checks; both tested natively in CI.

## Public contract

```csharp
var options = new DeliveryCheckpointOptions(statePath, new DeliveryChannel(consumerId, sourceId, routeId));
using var checkpoint = await DeliveryCheckpoint.OpenAsync(options, ct);          // OpenExistingAsync for inspection/grants
CheckpointState state = await checkpoint.ReadAsync(ct);
await checkpoint.StageAsync(state.Position + 1, canonicalBytes, ct);
await checkpoint.MarkSendingAsync(ct);                                            // immediately before the external effect
await checkpoint.AcceptAsync(canonicalBytes, acknowledgement, ct);                // or MarkUncertainAsync on failure
GrantOutcome g = await new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(requestId, expectation, itemId, receiptJson), ct);
```

## Units (test first, expected Red, minimal Green)

1. Open/identity/format (FR-003/004): tests open a fixture rebuilt from the Listener dump; Red: type missing → then wrong
   behaviour asserts; Green: CheckpointFile + CheckpointFormat + DeliveryCheckpoint.Open*.
2. Transitions (FR-006/007): CAS tests incl. bounds; Green: transition methods.
3. Privacy and lease (FR-005): native assertions; Green: CheckpointFile privacy branches.
4. Grants (FR-008…013): fixture-based idempotency/unique consumption/capacity/rollback; Green: GrantJournal + three classes.
5. Cancellation/crash (FR-014/015).
6. Registration and packaging (FR-016/017): coverage scope, quality suites, layout check (Red: layout refuses eight packages),
   isolated consumer script.

## Host adapter steps (for the integration owner, not implemented here)

Map `ListenerConfig` → `DeliveryChannel(InstanceId, ProjectId, Store.Route(config))`; keep `Notification.Json()` bytes as the
payload; build host key records as today and map them to `CheckpointExpectation`; serialize receipts with `Wire.Json` and pass
the text; map `DeliveryRefusal` to the current operator codes; keep feed comparison, readiness, configuration reload and umask.
