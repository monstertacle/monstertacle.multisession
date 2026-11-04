# Tasks

- [x] T001 Inventory and decisions (research.md; joint contrary review in SharpClaw specs/007).
- [x] T002 Refusal coverage: format1 (user_version 1, SchemaVersion 1) and a non-UTC stored timestamp. No Red: both were already refused by current code (format checks, snapshot consistency against indexed columns); the tests close the coverage gap left by removing the prior-package probe. The converter tolerance is unobservable and stays unchanged.
- [x] T003 Remove probe, migration guide, legacy wording (incl. docs/operations.md migration-plan sentence); converter unchanged (see T002); version 0.2.0-alpha.5; lock files.
- [x] T004 Local Linux quality; push; native CI; canonical alpha.5 feed in evidence/alpha5-feed.json.
- [x] T005 SharpClaw consumes alpha.5.

T005 (2026-11-01): SharpClaw consumes the canonical alpha.5 bytes on its branch feature/007-current-version-only (ef395cf); its regression is recorded there.

- [ ] T006 StopCallbackDeadlineTests.ReportsDeadlineWhileCancellationCallbackIsBlocked timed out once on windows-latest (run 41305730726, first attempt; rerun passed). Owner: integration maintainer. Scope: find whether the deadline or the test wait is too tight under a loaded runner; fix the responsible boundary without widening product limits. Acceptance: cause recorded; 20 consecutive Windows runs of that test class pass in CI.
  - Recurrence 2026-11-04:
    - Run: pull_request quality run 41793346681, `windows-latest`, attempt 1, head `4d25064` (pull request #34). That head
      changes only PlatformTests and specs; ContractTests are unchanged from `main` `558f9b1`.
    - Failure: `ReportsDeadlineWhileCancellationCallbackIsBlocked(callbackThrows: False)` threw `TimeoutException` at
      `StopCallbackDeadlineTests.cs:47`, the 5 s wait for the blocked cancellation callback to be entered.
    - The push quality run 41793336199 of the same head passed. Nothing was retried.
    - This recurrence is bound to this task. No cause is claimed and no bound is changed.
  - Source analysis 2026-11-04 (read-only, at `main` `e6fec23`; owner of this task unchanged):
    - **Observed in both failures:**
      - run 41305730726, attempt 1 (`b35dc7d`) and run 41793346681 (`4d25064`) fail only `callbackThrows: False`, with
        `TimeoutException` at `StopCallbackDeadlineTests.cs:47`;
      - the test file is byte-identical at both heads and on `main`.
    - **Line 47 is not the product deadline.** It waits up to 5 s for the provider's cancellation callback to be entered. The
      100 ms is the `StopAsync` deadline; only the result at line 48 depends on it.
    - **Source path to the callback** (`RuntimeLifecycle.StopOnceAsync`, the same order at both heads):
      1. the 100 ms deadline is created (line 113);
      2. `Operations.Stop()` runs; it cancels public operations, not the dispatch token;
      3. the stop gate is entered with the deadline token, `_shutdown.EnterAsync(linked.Token)` (line 123);
      4. only inside `CompleteStopAsync` does `StopWorkers()` call `RuntimeShutdown.CancelWorkers`, that is
         `CancellationTokenSource.CancelAsync()` on the lifecycle `_stop` token, which is the provider's token. That runs
         the callback on a thread-pool thread.
    - **Hypothesis H1 (source-supported, not measured):**
      - If 100 ms pass on the stop thread between steps 1 and 3 (a scheduling stall), the gate wait sees a cancelled token.
        `SemaphoreSlim.WaitAsync` checks the token before the free count; this is to be confirmed by control D1.
      - The stop then returns `StorageUnavailable` at line 124 without ever requesting worker cancellation. The callback is
        never entered, and the test times out at line 47.
      - Product question for the maintainer: is a stop whose deadline expires before it enters the gate meant to skip worker
        cancellation entirely?
    - **Hypothesis H2 (scheduling, not measured):** `CancelWorkers` ran, but the `CancelAsync` work item that runs the
      callback was not scheduled within 5 s.
    - **What the logs do not show:** whether `stopping` had completed, and whether the provider token was cancelled. They do
      not separate H1 from H2.
    - **Next executable step**, in this order and without changing any bound:
      - D1, a deterministic private control: run this test with a `TimeProvider` whose 100 ms timer fires at once. Expected
        under H1: the stop fails, the callback is not entered, and line 47 times out. This shows the mechanism is sufficient;
        it does not show it happened on the runner.
      - D2, a test-only discriminator for the next natural failure, if root grants it: on the line-47 timeout, report
        `stopping.Status` with its exception code and the captured provider token's `IsCancellationRequested`. Not
        requested means H1; requested but callback not entered means H2.
      - Then a decision by the maintainer and root. If H1 holds and the contract requires it, the fix is to request worker
        cancellation independently of gate admission. That is a product change needing its own Red; it widens no limit.
    - Acceptance stays as above: the cause recorded, and 20 consecutive Windows runs of the class passing in normal CI. No
      runs are started to find a failure.
  - D1 result 2026-11-04 (private control, granted by root; nothing retained in the repository):
    - **Setup:**
      - a private copy of `7c55c3e`; the product is unchanged;
      - the real `SessionRuntime`, receiving the clock through its existing public `TimeProvider` parameter;
      - the same provider, callback and composition as `ReportsDeadlineWhileCancellationCallbackIsBlocked(false)`.
    - **The injected clock** fires at once only the first 100 ms timer created on the stop call's own async flow. Every
      other timer goes to the system clock. Each run logged the timers: 600000 ms, 5000 ms, 30000 ms and the stop's 100 ms,
      and only the stop's timer fired.
    - **Results in three runs, all the same:**
      - *Expired deadline:* `StopAsync` failed with `StorageUnavailable`/`Unknown` after 0.2-0.5 ms. The provider token was
        not cancelled, at the stop result or 5 s later. The callback was not entered within 5 s, which is the line-47
        `TimeoutException` signature.
      - *Healthy clock:* `StopAsync` failed with `StorageUnavailable`/`Unknown` after 104-105 ms. The token was cancelled
        and the callback entered, which is the behaviour the test expects.
      - In both cases, the second `StopAsync(5 s)` cancelled the provider, the callback ran and the lease was disposed.
      - Separately, `SemaphoreSlim.WaitAsync` with an already cancelled token and a free count gave `Canceled`, with the
        count kept at 1.
    - **Established:** if the stop deadline has expired when `StopOnceAsync` reaches the gate (line 123), the stop returns at
      line 124 without requesting worker cancellation. That alone reproduces the observed failure.
    - **Not established:** that this happened on the Windows runners; that needs a stall of 100 ms or more inside
      `StopOnceAsync` before the gate. H2 is not excluded. The Windows cause stays unknown.
    - **Contract question for the maintainer and root:** `StopAsync` reports `StorageUnavailable` with `Unknown` certainty in
      both cases. But only the healthy path has requested provider cancellation. Should a stop that fails its deadline before
      entering the gate still request worker cancellation (as `Operations.Stop()` already does for public operations), or is
      leaving the workers running until the next stop or dispose the intended contract?
    - **Minimal proposals for root**, none implemented:
      - (a) a contract test, Red first: an expired-deadline stop must still cancel the dispatch token. It would use the same
        public `TimeProvider` injection.
      - (b) if (a) is the wanted contract: call `StopWorkers()` before or independently of the gate admission in
        `StopOnceAsync`. No deadline or bound changes, and the gate's single-stop ordering stays.
      - (c) if the current contract is kept: `StopCallbackDeadlineTests` should not rely on the 100 ms deadline outliving
        gate admission. That would be a test change with its own decision. Widening the 100 ms deadline is not proposed.
    - D2, the test-only discriminator for a natural failure, stays ungranted. The acceptance above is unchanged.
