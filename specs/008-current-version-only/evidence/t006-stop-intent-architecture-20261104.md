# T006 stop intent: architecture before implementation (2026-11-04)

Part of `tasks.md` T006. Root contract 008T006-STOP-INTENT-CONTRACT-20261104 adopts a prospective guarantee:

> After a valid stop passes the existing state guards and publishes `Stopping`, it requests the available worker-token
> cancellation through the existing guarded, idempotent `StopWorkers`, whether or not stop-gate admission succeeds. It never
> waits for callbacks outside the bounded stop and drain.

This is a deliberate strengthening. It does not claim that the old contract was violated, and it does not explain the Windows
failures.

## Source state

- `main` `4950b82`. Pins match the root decision:
  - `RuntimeLifecycle.cs` `9e092ebe…`;
  - `RuntimeShutdown.cs` `069c219c…`;
  - `StopCallbackDeadlineTests.cs` `34d62ca9…`.
- Branch `fix/008-t006-stop-intent`.

## Code examined

**`RuntimeLifecycle.StopOnceAsync`** (lines 110-132)
1. Invalid timeouts are refused, and the deadline is created (line 113).
2. Under `_gate`: `Disposed` returns, `Stopped` replays, `Created` and `Starting` are refused, and `Stopping` is published (line 120).
3. `_shutdown.Operations.Stop()` cancels public operations before the gate (line 122).
4. The stop gate is entered with the deadline token (line 123). If the deadline has expired or the caller has cancelled, the gate
   wait fails and the method returns `StorageUnavailable`/`Unknown` (line 124).
5. Only inside `CompleteStopAsync` (lines 134-157) does `StopWorkers()` run: after `StopAcceptanceAsync`, and again after the
   queued cancellation.

**`StopWorkers()`** (lines 197-203)
- Under `_gate`, it skips `Stopped`, `Disposed` and a null `_stop`, then calls `RuntimeShutdown.CancelWorkers(_stop)`.
- `CancelWorkers` (`RuntimeShutdown.cs:18`) is `_workerCancellation ??= source.CancelAsync()`. It is idempotent, and it runs
  callbacks on the thread pool without waiting for them.

**Callback faults and draining**
- The cancellation task is awaited only in `RuntimeShutdown.DrainAsync`, from `CleanupCoreAsync` inside the bounded stop
  (`CleanupAsync().WaitAsync(token)`).
- A faulted cancellation (a throwing callback) becomes `StorageUnavailable` through `RequireSuccessfulCancellation` after
  cleanup.

**Ownership order** (`CleanupCoreAsync`, lines 210-223)
- drain, then statistics, then store, then lease;
- then, under `_gate`, `_signal` and `_stop` are disposed and `Stopped` is published.

**Token scope**
- The `_stop` token is used only by the automatic workers (`WorkAsync`, line 93, through `RuntimeWorker.RunAsync`).
- HostControlled mode has no workers, so its dispatch policy does not depend on this token.
- `RuntimeOperations` holds its own token.

**C1b** (`specs/003-native-reliability/follow-up-dispose-stop-race.md`)
- C1b concerns a `Faulted` runtime without components, where `_stop` is null, so `StopWorkers` is a no-op there.
- `_stop` is disposed only together with publishing `Stopped`, under the same lock that `StopWorkers` checks.
- An early guarded call therefore adds no access to a disposed token source.
- C1b stays open and unchanged.

**Existing tests that touch stop timing:** the ContractTests ownership, cleanup, dispose and overlap suites, and the
`RuntimeLifecycleTests` integration suite. They must stay green unchanged.

## Decision

- Add one call to the existing `StopWorkers()` in `StopOnceAsync`, after `_shutdown.Operations.Stop()` and before the gate.
- The later calls in `CompleteStopAsync` stay. They are no-ops after the first request, because of `??=`.

**Unchanged:**
- no new API, state or format;
- no deadline, default or wait change;
- the cleanup and ownership order;
- the failure mapping;
- the guards for invalid timeout, `Created`, `Starting`, `Stopped` replay and `Disposed`;
- the HostControlled policy.

**Effect on the normal path:** cancellation is requested slightly earlier, before `StopAcceptanceAsync` completes. Public
submissions are already refused once `Stopping` is published (`RunOperation` requires `Running`). A worker could previously still
reserve a queued head in that short window; it would then be cancelled moments later by the existing `StopWorkers`. No new state
becomes possible; only that window shrinks.

## Test plan

**New `StopIntentCancellationTests`** (ContractTests). The theory covers a pre-cancelled caller and a pre-expired 100 ms deadline,
each with a non-throwing and a throwing held callback. The deadline is expired through the public `TimeProvider` parameter, using a
test clock that fires only the stop's own 100 ms timer.

Each case asserts:
- the stop fails with `StorageUnavailable`/`Unknown` within 2 s;
- the callback is entered within 5 s and the provider token is cancellation-requested;
- the lease is still held while the callback is blocked;
- after release, a second stop drains: it returns without error, or fails with `StorageUnavailable` for a throwing callback;
- the lease is disposed after dispose.

**Red** on the unchanged product: the callback is never entered, and the token is not requested.

**`StopCallbackDeadlineTests` stays byte-unchanged.** With the fix, a pre-gate deadline expiry can no longer produce its line-47
timeout. The remaining hypothesis H2 (thread-pool scheduling) cannot be removed by any clock, and no wall-time guarantee under
starvation is claimed. Stage alignment is therefore not required.

**Mutations:**
- remove the early call;
- wait synchronously for the callbacks before the gate;
- release ownership early in the gate-failure path;
- swallow a faulted cancellation.

**Gates:** format, full Codemetr, full Linux quality with coverage of 87 % or more, then exact-head native Linux and Windows CI.
