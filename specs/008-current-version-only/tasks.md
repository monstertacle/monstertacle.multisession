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
  - Root contract 2026-11-04 (008T006-STOP-INTENT-CONTRACT-20261104):
    - After a valid stop passes the state guards and publishes `Stopping`, it requests worker cancellation through the
      existing guarded, idempotent `StopWorkers`, whether or not it enters the stop gate. It never waits for callbacks outside
      the bounded stop and drain.
    - A pre-cancelled caller cancels the waiting, not the stop intent.
    - Failure mapping, guards, replay, ownership order and the HostControlled policy are unchanged.
    - This is a prospective strengthening, not a claim that the old contract was violated.
  - Implemented on `fix/008-t006-stop-intent` (evidence/t006-stop-intent-architecture-20261104.md):
    - one call to `StopWorkers()` in `RuntimeLifecycle.StopOnceAsync`, after `Operations.Stop()` and before the gate;
    - new `StopIntentCancellationTests`: a pre-expired 100 ms deadline, through the public `TimeProvider`, and a pre-cancelled
      caller, each with a non-throwing and a throwing held callback.
    - Red on the unchanged product: 4 of 4 failed with "the stop did not request worker cancellation".
    - Green: 4 of 4, and the existing stop, ownership, cleanup, overlap and dispose suites (24 of 24) passed unchanged.
    - Mutations, all killed: removing the early request; waiting synchronously for the callbacks; releasing the lease early;
      swallowing a faulted cancellation.
    - `StopCallbackDeadlineTests` is byte-unchanged and was not stage-aligned. With the fix, a pre-gate expiry can no longer
      produce its line-47 timeout, and the remaining scheduling hypothesis H2 cannot be removed by a clock.
  - Still open:
    - the Windows cause (H1 or H2, not measured on a runner) stays unknown;
    - D2 stays ungranted;
    - acceptance still needs the cause recorded and 20 consecutive normal Windows runs of the class.
    Owner: integration maintainer; root accepts and merges.
  - Review finding NEC-T006-TEST-CLEANUP-001 (alias DEF-T006-TEST-CLEANUP-001), accepted by root as an acceptance blocker:
    - The finding: in the first version of `StopIntentCancellationTests`, the `finally` only released the callback. Any assertion
      exit skipped the drain stop and the runtime disposal before the test lease was disposed.
    - The repair, in that test only:
      - every exit after the runtime start runs the original cleanup order: release the callback, observe the first stop within
        5 s, drain with `StopAsync(5 s)`, then dispose the runtime, all before the test lease is disposed;
      - the original failure is rethrown unchanged;
      - an unexpected cleanup failure is reported after it, in an `AggregateException`.
    - Unchanged: the product (`RuntimeLifecycle.cs`), `StopCallbackDeadlineTests` and the 100 ms, 2 s and 5 s limits.
    - Red: a new forced-exit proof, with exits after start, after the stop failure and after the drain, plus a failing store
      disposal. It failed 4 of 4 on the first cleanup shape.
    - Green: 8 of 8, the four contract cases and the four proofs.
    - Mutations:
      - killed: cleanup skipped on failure; cleanup failure placed before the primary; cleanup failure swallowed; runtime
        disposal skipped (3 of 4, with one equivalent case);
      - two first variants did not compile and are not counted;
      - skipping the observation of the first stop survived, and is equivalent with the current exit points;
      - the product mutation without the early request still fails with its own primary message.
    - The Windows cause, D2 and the 20-run acceptance above stay open.
  - Merge receipt 2026-11-04: root merged pull request #36 normally as `53cb6af` (parents `4950b82` and `239b589`; tree
    `e877de6a` equal to the qualified `239b589`). All 12 exact-head native checks passed on their first attempt.
    - NEC-T006-STOP-INTENT-001 is implemented: the stop requests worker cancellation before the gate.
    - NEC-T006-TEST-CLEANUP-001 is resolved in that source.
    - The fix is in source only. It is not yet in a published package or in the host; see T007.
  - D2 granted by root 2026-11-04, prospectively, for the next natural failure:
    - Implemented in `StopCallbackDeadlineTests`, test only. When the line-47 callback-entry wait times out, the test writes one
      line, `t006-d2 observation`, then rethrows the original `TimeoutException` unchanged. The line holds:
      - the stage;
      - `stopping.Status`, and the failure code and certainty if the stop has already failed;
      - whether the provider token was captured, and if so whether cancellation was requested;
      - whether the callback was entered.
    - All values are read without waiting. The 100 ms, 2 s and 5 s limits, the cleanup and both callback cases are unchanged. A
      failing write is thrown together with the timeout, which stays first.
    - Reading the line: since the early worker request is merged, a token without a cancellation request can also mean that
      the stop had not yet run or reached that request. The line therefore names no cause and does not separate H1 from H2.
    - Evidence, through private controls in which the callback entry is never signalled:
      - the unchanged test gave a line-47 `TimeoutException` and no line (Red);
      - the new test gave the same timeout and one line per test result;
      - a failing write gave `AggregateException` with the timeout first;
      - the controls detected two mutations: not rethrowing the timeout (the test then passed), and the write failure
        replacing the timeout. No retained test detects them; only the private controls do.
  - Still open: the historical Windows cause, which no later run can assign, and the acceptance of 20 consecutive normal Windows
    runs. No CI runs are started to find a failure or to reach 20. Owner: integration maintainer; root accepts.
  - Merge receipt 2026-11-04: root merged pull request #37 normally as `4ab04c4` (parents `53cb6af` and `ce923b1`; tree
    `1eb5cec4`, equal to the qualified head).
    - That merge adds only the D2 test diagnostic, which is for a future natural failure. The product fix is the earlier
      stop-intent change from pull request #36.
    - Still open in this task: the historical Windows cause, which no later run can assign, and the acceptance of 20
      consecutive normal Windows runs. The release of the source is T007.
- [ ] T007 Release of the T006 stop-intent fix. Owner: integration maintainer; publication only by root decision.
  - Source: `main` `53cb6af`, which contains the stop-intent change in `RuntimeLifecycle.StopOnceAsync` and
    `StopIntentCancellationTests`. The published 0.2.0-alpha.9 packages do not contain it and stay immutable.
  - Proposal: the next prerelease, 0.2.0-alpha.10, built from an exact, root-accepted `main` commit that contains `53cb6af`. The
    version is not changed in this task.
  - Next: root decides whether and when to publish. Then:
    - a version change on a topic branch;
    - the full native Linux and Windows quality, coverage, Codemetr and canonical-consumer gates on that exact head;
    - publication through the existing producer workflow.
  - Acceptance:
    - the publication receipt and an independent served-package check (hashes and versions of the packages as served);
    - the host consumes 0.2.0-alpha.10 in a separate host task, with its own gates;
    - until then the fix counts as source only.
  - Dependencies: a root publication decision; the T006 items stay separate.
  - Source candidate 2026-11-04 (root grant: preparation and qualification only):
    - Branch `release/0.2.0-alpha.10-source`, from `main` `4ab04c4`. That commit contains the stop-intent fix (pull request
      #36) and the D2 diagnostic (pull request #37).
    - Changes:
      - the version in `Directory.Build.props` is 0.2.0-alpha.10;
      - the version headings in `README.md` and `docs/package-readme.md`;
      - in `docs/embedding.md`, a statement that 0.2.0-alpha.10 is an unpublished candidate and 0.2.0-alpha.9 is the latest
        published release, and a release note for the stop change;
      - the lock files, regenerated by restore: 69 own-project references change from 0.2.0-alpha.9 to 0.2.0-alpha.10, and no
        third-party package changes.
    - Unchanged: the nine package identities, product behaviour and tests, CI and release policy, and the 0.2.0-alpha.9
      record, artifacts and served packages.
    - Not done: no producer run on `main`, no release record, no publication, no tag and no host change. A release record is
      written only for bytes that the producer run on `main` has built.
    - Next, after root's source acceptance and normal merge:
      1. the `ci.yml` push run on `main` produces the packages;
      2. `eng/release-records/0.2.0-alpha.10.json` records that run through a normal pull request;
      3. root decides on publication, and `publish.yml` then runs from `main`;
      4. an independent served-package check follows;
      5. the host uptake is a separate host task.
  - Review finding NEC-A10-FEED-001 (alias DEF-A10-DOC-001), accepted by root as a source acceptance blocker:
    - The finding: in `docs/embedding.md`, the install command asked for 0.2.0-alpha.9 "from nuget.org or from a local feed of
      packages packed from this source", but this source packs 0.2.0-alpha.10. The shown mapping also sent the library's own
      packages only to the local feed, so the nuget.org alternative was not enabled.
    - The repair, in `docs/embedding.md` only, splits the guide into two routes, each with its own `NuGet.Config`:
      - published 0.2.0-alpha.9 from nuget.org, with the library's own packages mapped to nuget.org;
      - unpublished 0.2.0-alpha.10 packed from this source, with the library's own packages mapped only to the local folder,
        and a note that it is not on nuget.org.
      - The third-party mappings are unchanged.
    - Evidence, from private consumers in `~/dev/.agenttemp`, each with its own packages folder. Each uses the documented
      configuration and references Common, Storage.Sqlite and Linux:
      - published route: restore and build pass, with all three packages resolved as 0.2.0-alpha.9 from nuget.org. The served
        Common package matches the served digest in `publication-receipt-0.2.0-alpha.9.json`;
      - local route: restore and build pass, with all three packages resolved as 0.2.0-alpha.10. The Common package is
        byte-identical to the locally packed candidate;
      - old wording as a control: the local-only mapping with `--version 0.2.0-alpha.9` fails with NU1603. NuGet substitutes
        0.2.0-alpha.10 for the requested 0.2.0-alpha.9.
    - Unchanged: version, lock files, product, tests, CI and release policy. T007's next steps above are unchanged.
