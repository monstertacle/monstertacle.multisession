# Disposal after a timed-out stop can rethrow the earlier stop failure

Owner: Alexander Bleissem
Status: contract Q decided and merged into `main` (pull request #23, 2026-11-03). Item C1 has a root decision and is being
implemented on branch `fix/c1-retain-stop-gate` (not merged). Item C1b is an unconfirmed source-only candidate.
Current status (2026-11-04, supersedes the status lines above, which are kept as written): contract Q (pull request #23) and the
C1 correction (pull request #24) are merged, released in 0.2.0-alpha.9 and accepted; T017 and T018 are closed. C1b and M4 below
remain open.
Origin: push run 41699098083 at `4669501` (pull request #22). `quality (windows-latest)` failed one case,
`ContractTests.RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses`. The pull request run 41699106368 on the same
head passed the same test, as did the runs on `a24e548` and `8054481`. The runtime source is unchanged from `main` (`4b533c9`).

## Observed

The test's `finally` block calls `runtime.DisposeAsync()` after an intentionally timed-out `StopAsync`. It threw
`SessionOperationException: StorageUnavailable`:
- `RuntimeLifecycle.ThrowStopFailure` (`RuntimeLifecycle.cs:222`);
- called from `StopAsync` (`:111`, the locked check `_state == RuntimeState.Stopped`);
- called from `DisposeCoreAsync` (`:183`).

## Cause (reproduced on Linux; the Windows scheduling itself is not reproduced)

`DisposeCoreAsync` reads `_state` without the lock (`:181`) and calls `StopAsync` when the state is not yet `Stopped`. The only
writer of `Stopped` is the shared cleanup (`CleanupCoreAsync`, `:212`). If the cleanup of the earlier, timed-out stop publishes
`Stopped` between those two points, `StopAsync` takes the `Stopped` branch and rethrows the recorded stop failure. If cleanup
finishes before `:181`, disposal returns normally. The same history therefore ended with or without an exception.

`ContractTests/DisposeAfterStopFailureTests.IsQuietWhenCleanupCompletesAfterDisposalBegins` reproduces exactly that stack. It
uses only the public `TimeProvider` parameter of `SessionRuntime` (`Fixtures/DeadlineControlClock`):
- the timed-out stop's deadline is expired on demand;
- the creation of the disposal's own stop deadline, which lies after `:181` and before the stop lock, is held;
- the held store cleanup is then completed.

The test thus depends on where the disposal creates its deadline. If that point moves, the scheduling control has to be
revisited even when the contract still holds.

## Decision: contract Q (root, 2026-11-03, after two opposed reviews)

- `DisposeAsync` does not replay an earlier stop failure merely because that stop's cleanup published `Stopped` first.
- The public `StopAsync` caller that failed keeps its failure. A later valid public `StopAsync` call that observes the stopped
  state replays the failure recorded at that time, if any. This is not a permanent failure history: a stop that completes the
  shutdown records its own outcome and can replace the record, and a failure while waiting for another stop is reported only to
  that caller.
- Failures of the stop that disposal performs itself still propagate: its own deadline, gate wait or cleanup failure.
- After disposal, a valid `StopAsync` call does nothing.

Rejected: contract R, where disposal always re-reports a recorded failure. It contradicts existing callers that dispose after
handling a stop failure (`RuntimeOwnershipTests`, `StopCallbackDeadlineTests`, `RuntimeLifecycleTests`). Also rejected: catching
exceptions in disposal, and a new production synchronization hook.

## Change

`RuntimeLifecycle`:
- A private `StopOnceAsync` returns `true` exactly where the old code replayed the recorded failure: `Stopped` at the first
  locked check, and `Stopped` or `Disposed` after the stop gate is acquired.
- The internal `StopAsync` rethrows in that case.
- `DisposeCoreAsync` calls `StopOnceAsync` and does not replay.

Unchanged: the stop deadline, the gate, cleanup sharing, the store-before-lease order, error codes, persistence and the API.

## Evidence (Red, Green, negative controls)

- **Red** on the unchanged runtime (`c652930`): the ordering test failed with `ThrowStopFailure :222` called from `StopAsync :111` called from
  `DisposeCoreAsync :183`. The other four tests passed: they pin existing behaviour.
- **Green** with the change: all five tests pass.
  - `IsQuietWhenCleanupCompletedBeforeDisposal` also checks that a repeated `StopAsync` still reports the failure, and that
    `StopAsync` after disposal does nothing.
  - `ReportsItsOwnDeadlineWhenDisposalPerformsTheStop` and `ReportsItsOwnCleanupFailureWhenDisposalPerformsTheStop` check that
    disposal reports its own failures and keeps the lease. The cleanup failure is reported sanitized.
  - `SharesOneDisposalAndCleanupAcrossConcurrentDisposeCalls` checks one shared disposal task, one store cleanup, and that the
    lease is still held while the store closes.
- **Mutations** of the changed source:

  | Mutation | Result |
  | --- | --- |
  | Catching all exceptions around disposal's stop | Both own-failure tests fail |
  | Always replaying in disposal | 3 tests fail |
  | Replaying after `Disposed` at the first check | 1 test fails |
  | Replaying at the second check (after the gate) | **Survives** (see Limits) |

- `RuntimeCleanupTests` and all other existing tests are unchanged.

## Limits

- **Second window not controlled.** `Stopped` published between taking the lock and the check after acquiring the gate (`:113`
  to `:120` in the old source) has no external control point: the only seam is contention on the internal stop gate, whose
  waiter position cannot be observed. Its handling is accepted from source only, and the surviving mutation records that gap.
- **Windows cause not proven.** The Linux reproduction does not prove why the Windows runner scheduled the race.

## C1: a stop still unwinding while disposal completes (open)

**Reproduced, pre-existing, not caused or fixed by contract Q.**

The scenario:
1. Cleanup publishes `Stopped` (`:212`) while a public `StopAsync` still holds the stop gate. It releases the gate only in its
   `finally` (`:123`).
2. A concurrent `DisposeAsync` observes `Stopped` at `:181`, skips the stop and disposes the shutdown resources, including the
   stop gate.
3. The unwinding stop then calls `SemaphoreSlim.Release` on the disposed gate.

The result is that the public `StopAsync` call ends with a raw `ObjectDisposedException` instead of its own outcome, on both the
old and the changed source.

What still holds in that run: one store cleanup, the store closed before the lease, and the lease released.

How it was reproduced: a private probe holds the stop's continuations in a caller-owned `SynchronizationContext`. The same
technique is now committed as `ContractTests/StopDisposeOverlapTests` (see C1 evidence).

Severity P2: only callers that dispose without awaiting a running `StopAsync` are affected, but they get an untyped, unsanitized
exception. `docs/operations.md` describes the caller contract after the C1 correction.

**Task C1** (owner Alexander Bleissem):
- **Decision:** root decides the correction. Candidates are that disposal waits for a running stop to release the gate before
  disposing shutdown resources, or that the stop's gate release tolerates disposal. Either must keep the stop deadline and the
  lease order.
- **Then:**
  - add the overlap probe as a red-first regression;
  - make the smallest change;
  - cover concurrent stop waiters on the gate as well;
  - run the full native gates.
- **Acceptance:** the overlapping `StopAsync` ends with its own typed outcome, disposal stays bounded, there is still one
  cleanup, and the store-before-lease order holds.
- **Not done:** not closed by the contract Q change, and not closed by a green CI run. The correction is decided and implemented on
  `fix/c1-retain-stop-gate` (see C1 decision and C1 evidence); it stays open until review and merge.

### C1 decision (root, 2026-11-03, after two opposed architecture reviews)

Source at `main` `dd1ca0e`: `RuntimeLifecycle.cs` SHA-256 `9e092ebe...`, `RuntimeShutdown.cs` `89c533e8...`,
`RuntimeOperations.cs` `d8bfad56...`.

- **Chosen, the minimum correction:** `RuntimeShutdown.Dispose` no longer disposes the private stop gate (`_stopGate`, a
  `SemaphoreSlim`). The gate stays usable for stops that were admitted earlier: the holder can release it, and waiters can enter
  and observe `Stopped` or `Disposed`. The gate is only used through `WaitAsync` and `Release`; nothing reads
  `AvailableWaitHandle`, so it holds no wait handle to release. Disposal of the operations token source and all other cleanup is
  unchanged.
- **Not chosen:** a disposal drain or barrier that waits for running stops. It would be a new, stronger guarantee that the
  contract does not require, and the proposed placement missed the earlier replay path. Also rejected: catching
  `ObjectDisposedException` at `Release`, which leaves queued waiters stranded and hides the lifetime error, and moving where
  `Stopped` is published.
- **Contract after the fix:** `DisposeAsync` can complete before an overlapping `StopAsync` call has returned. That call then
  ends with its own typed outcome. Disposal does not promise that all stop calls have returned.

## C1b: stop admitted after an unprepared start while disposal releases the operations token (open, unconfirmed)

Origin: the C1 necessity review (2026-11-03), from source at `main` `dd1ca0e` (hashes above). It is not reproduced, not a
confirmed defect, and not part of the C1 correction.

The candidate sequence:
1. Start-up fails during store-side option validation or store start, before the store has started. Early
   `RuntimeOptions.Validate()` rejection is outside this sequence because it occurs before the start-up try block and does not
   set `Faulted`. In this sequence the state becomes `Faulted`, `_components`
   stays null, and no cleanup runs (`StartAsync` cleans up only once the store has started), so
   `RuntimeOperations.Stop` was never called.
2. A public `StopAsync` passes its first locked check (`Faulted` is admitted), sets `Stopping`, and is descheduled before it calls
   `_shutdown.Operations.Stop()`.
3. A concurrent `DisposeAsync` takes the `_components is null` branch, publishes `Disposed` and disposes `RuntimeOperations`,
   including its token source.
4. The stop resumes, and `Operations.Stop()` calls `CancelAsync` on the disposed token source.

On the normal path this cannot happen, because cleanup drains the operations, which initialises the cancellation, before
publishing `Stopped`.

External control: between leaving the lock and calling `Operations.Stop()` there is no await and no call into a host-supplied
dependency. No existing public seam (clock, store, provider or synchronization context) can hold the stop there; only thread
preemption can. A deterministic probe therefore needs either a new seam, which needs an architecture decision, or a source-level
argument.

**Task C1b** (owner Alexander Bleissem; root decides):
- **Next:** confirm or refute the sequence with a bounded private probe, if a deterministic external control is found. Otherwise
  record a source-backed disposition. Possible corrections, only after evidence and a separate decision, are making the
  operations cancellation part of the stop's admission, or coordinating `RuntimeOperations.Stop` and `Dispose`.
- **Acceptance:** either a source-and-probe-backed safe disposition, or an observed Red followed by a separately decided minimal
  correction. In the corrected state there is no raw token-source disposal exception, the typed stop semantics are unchanged, and
  host-owned dependencies are not disposed before ownership transfers.
- **Not done:** not closed by C1, and not closed by a green CI run.

### C1 evidence (branch `fix/c1-retain-stop-gate`, base `main` `dd1ca0e`)

`ContractTests/StopDisposeOverlapTests` holds a public stop's continuations in a caller-owned synchronization context
(`Fixtures/QueuedSynchronizationContext`). This depends on the lifecycle's awaits capturing the caller's context; adding
`ConfigureAwait(false)` there would remove the control. No production hook was added.

- **Red** on the unchanged runtime:
  - a successful stop that is still unwinding ended with `ObjectDisposedException` from `SemaphoreSlim.Release`;
  - a stop with its own failed worker cancellation ended with `ObjectDisposedException` instead of its sanitized
    `StorageUnavailable`;
  - a stop that started waiting for the gate before `Stopped` was published never finished. The guard ended the test while the
    stop's deadline was still unexpired.
  - The fourth test passed (a queued stop's own deadline is reported), and it pins existing behaviour.
- **Green** after removing the gate disposal: all four pass, and so do the contract Q, cleanup, callback and ownership tests.
- **Mutations:**

  | Mutation | Result |
  | --- | --- |
  | Restoring the gate disposal | 3 tests fail |
  | Disposing the gate and ignoring `ObjectDisposedException` at `Release` | The queued-waiter test still fails |

- **Not covered:**
  - a stop that passes its first check after disposal began but before `Disposed` is published, beyond the queued-waiter case;
  - the contract Q survivor at the post-gate check;
  - C1b.

  The fix does not claim general concurrency safety for stop and disposal.

## Addendum 2026-11-04: closure and remaining tasks

Root accepted contract Q and the C1 correction with the published 0.2.0-alpha.9:
- `main` producer run 41738827195 at `67a7de9`;
- publish run 41742503760;
- receipts in `specs/006-host-execution-authority/evidence/`.

The sections above describe the state at the time they were written. Their "open" and "not merged" wording for C1 is
historical.

**Task M4** (owner Alexander Bleissem; open, accepted as a source-only limit until resolved). Replaying at the check after the
stop gate survives the contract Q tests, because that window has no external control point.
- **Next step:** look for an existing public seam that can hold a stop between taking the lifecycle lock and the post-gate check.
  If none exists, record a source-backed disposition for root.
- **Dependency:** a new seam needs an architecture decision; no production hook without it.
- **Acceptance:** either a deterministic test that kills the M4 mutation, or a recorded root disposition that accepts the
  source-only coverage.

**Task C1b** stays as recorded above: owner Alexander Bleissem, unconfirmed, with its next step and acceptance unchanged.
