# Windows elapsed-time bounds failed on a documentation-only head (T019, T020)

Owner: Alexander Bleissem (diagnosis and source uptake); root (source decision, reruns and merge)
Status: open; cause unknown for both. Recorded 2026-11-04. Priority P1 for each: a required native check is red. No product effect is
shown.
Origin: findings SDK-WIN-CANCEL-LATENCY-20261104 (T019, attempt 1) and SDK-WIN-CLEANUP-LATENCY-20261104 (T020, attempt 2), pull
request #28 (`cc319fe`, which changes only
`specs/006-host-execution-authority/tasks.md`; runtime and tests are byte-identical to `main` `1bdac2e`).

## Observations (both on `quality (windows-latest)`, pull request run 41754114699)

The two attempts failed different assertions in different suites. They are recorded separately, and no shared cause is assumed.

| Attempt | Job | Failed test | Bound | Other suites |
| --- | --- | --- | --- | --- |
| 1 (T019) | 126326035132 (01:26:54Z to 01:36:07Z) | `IntegrationTests.EmbeddedConsumerTests.RunsCompletePublicEmbeddingFlowWithoutLibraryChangesOrServerProcess`, line 49 (1 of 436) | cancelling a running request must reach its end within 2 seconds | UnitTests 447 and ContractTests 266 passed |
| 2 (T020) | 126331776929 (01:42:34Z to 01:48:29Z) | `ContractTests.RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses`, line 29 (1 of 266) | a stop with a 200 ms deadline, while store disposal is held, must return its failure within 1 second | UnitTests 447 passed; IntegrationTests did not run, because the gate stops at the first failed suite |

- **Same head, push run:** the push quality run 41754108932 at the same head passed both tests on Windows, and both pass on Linux.
- **No elapsed values:** neither assertion logs the measured elapsed time, so the size of the overrun is unknown.
- **Log hashes** (SHA-256 of the failed-job logs, kept privately): attempt 1 `5d1ea024...909b`, attempt 2 `7ee69744...973f`.

## Paths measured

- **Cancellation** (`EmbeddedConsumerTests.cs:46-49`):
  - the timer starts before `Requests.CancelRequestAsync`, which records the cancellation durably;
  - the dispatcher cancels the provider token, and `ConsumerOwnedProvider` ends its delayed request as cancelled;
  - the store commits the confirmed end;
  - the test helper polls the request every 10 ms (`EmbeddedConsumerTests.cs:69-76`).
- **Stop deadline** (`RuntimeCleanupTests.cs:22-29`):
  - the timer starts before `StopAsync(200 ms)`;
  - the stop stops acceptance and cancels the workers;
  - it waits for the worker completion, then starts the shared cleanup, whose store disposal the test holds;
  - the 200 ms deadline must end the wait with `StorageUnavailable`.

  This bound concerns the deadline, not the cleanup duration. It is the same test as the earlier stop/dispose finding, but a
  different assertion: line 29, the elapsed bound, not disposal.

## Next steps

1. **Decide without rerunning.** The next native run of this head, or a fresh run, is root's decision. Do not rerun to get green.
2. **Diagnostic plan, pure C#, outside the repository** (under `~/dev/e2e-harness-bundles`, as in `follow-up-windows-admission.md`):
   - run both scenarios with per-stage timestamps (cancellation record, provider token cancellation, provider end, completion
     commit, read; stop entry, worker completion, cleanup start, deadline firing, stop return);
   - run on native Windows and on a Linux control, with fixed packages;
   - record the overrun and the slow stage.
3. **No change to a bound or to runtime code** without a measured envelope or an isolated defect. A defect needs an observed
   regression Red first.

## Required native tests

Both tests, unchanged, in a native Windows run of the decided head, plus the complete quality gate. If a defect is isolated, its
deterministic regression goes on both platforms.

## Acceptance

The slow stage of each observation is measured, or the remaining uncertainty is stated with the measured envelope, and root
decides the gate. Both 2 s and 1 s bounds stay as they are unless an envelope decision changes them. This record does not claim a
cause.

Related: `follow-up-windows-admission.md` (Windows admission variability), `follow-up-capacity-probe.md` and
`follow-up-dispose-stop-race.md`. No shared cause with them is established.

## Measurements 2026-11-04 (passing runs only)

- **Instrumentation (historical, kept as evidence):** test-only stage logging on the observation branch
  `diag/t019-t020-stage-timing` (`22ab323`, not for merge).
  - The two tests each wrote one stage line, and the provider fixture only stored a timestamp.
  - The lines were written before the unchanged bounds, so formatting and output ran inside the measured windows, and nothing was
    written when an earlier step failed.
- **Runs:**
  - Linux: 20 local runs of each test;
  - native Windows and Linux: one push run of `ci.yml`, 41762345411, all jobs successful.
- **Values:** in milliseconds from the start of each measured window. Windows and Linux CI are single runs. The values are
  unchanged; the labels say what each stamp observed.

| Stage, as observed by the test | Linux local (min / median / max) | Windows CI | Linux CI |
| --- | --- | --- | --- |
| T019 `CancelRequestAsync` completion observed (logged as "cancellation recorded") | 7.0 / 8.7 / 21.0 | 19.1 | 1.2 |
| T019 provider observed cancellation | 91.8 / 96.7 / 101.2 | 87.8 | 99.0 |
| T019 terminal read returned (bound 2000) | 105.6 / 107.8 / 119.6 | 115.6 | 103.9 |
| T020 test resumed after the cleanup-entry wait (logged as "cleanup entered") | 7.6 / 8.5 / 13.4 | 2.3 | 1.1 |
| T020 stop task completion callback observed (logged as "stop returned"; deadline 200) | 200.6 / 204.6 / 213.0 | 201.5 | 200.7 |
| T020 snapshot before output, not the live assertion time (logged as "observed"; bound 1000) | 204.4 / 208.2 / 216.3 | 201.6 | 200.8 |

- **Normal envelope:** in normal runs both paths sit far inside their bounds. The cancellation is seen within one 100 ms poll, and
  the stop completes just after its 200 ms deadline.
- **Not measured:** the failing runs (pull request #28, run 41754114699). The stage that stalled there is unknown, and no cause is
  claimed.

## Permanent test-only observation (root decision 2026-11-04)

Root chose a revised, permanent test-only observation after two opposed reviews. It is not a runtime fix. It is on branch
`test/t019-t020-stage-observation`, separate from these records.

- **Unchanged:** both tests keep their timer origins, live elapsed assertions, deadlines, semantic assertions and, for T020, the
  cleanup order.
- **The line:** a `StageObservation` fixture writes one line after the measured window, from a `finally`, whether the window passed
  or failed.
  - It includes the outcome and the failure type.
  - A stage that was not reached is written as not observed.
  - A failing write is thrown together with the measured failure, which comes first.
- **No completion continuation:** the stop stamp is taken when the test's await of the stop failure returns. The provider timestamp
  is published and read atomically.
- **Limits:**
  - the line shows the last stages the test observed, not which internal runtime step was slow;
  - nothing is written if the process ends or an awaited step never returns;
  - passing runs do not explain the original failures.

T019 and T020 stay open with the cause unknown. The next natural failing native run is bound to them by root monitoring, with no
extra runs started to find one.

## Call-site failure controls (root grant 2026-11-04)

Two opposed reviews found that the helper tests throw synchronously and do not show the real tests failing. These controls fill
that gap.

**Setup**

- The probes ran on Linux, in Release, in a private copy of head `ac4aab8`. That copy had the same tree, `3973e38`.
- Each probe changed one test in that copy only. Afterwards the file was restored, and its SHA-256 matched the head again.
- No bound was loosened, and no probe change is kept in the source.

**The probes**

| Probe | Private change | Fails at | Stage line in the test's own TRX result |
|---|---|---|---|
| P1, T019 early await | `CancelRequestAsync` gets an already cancelled token | line 56, from the awaited `RequestCancellation.CancelAsync` | `failed:System.OperationCanceledException`, all three stages not observed |
| P2, T019 elapsed | bound `TimeSpan.FromSeconds(2)` becomes `TimeSpan.Zero` | the original assertion, line 60 | `failed:Xunit.Sdk.TrueException`: cancel-request-returned 10.0 ms, provider-cancellation-observed 95.1 ms, terminal-read-returned 110.9 ms |
| P3, T020 early wait | the store's `Disposing` no longer signals entry | the original 5 s entry wait, line 32 (test took 5.04 s) | `failed:System.TimeoutException`, both stages not observed |
| P4, T020 elapsed | bound `TimeSpan.FromSeconds(1)` becomes `TimeSpan.Zero`, plus order markers in the original `finally` | the original assertion, line 39 | `failed:Xunit.Sdk.TrueException`: cleanup-entry-wait-observed 14.2 ms, stop-failure-observed 209.0 ms |

**Results**

- **Failure kept:** each probe's test failed with its primary exception. The stage line was written once per test result.
- **Failure-only echo:** for a failing test, the xUnit adapter also copies the test output into the run summary and the console
  log. A passing test's line is only in its TRX result.
- **P4 cleanup order:** the markers show the original `finally` unchanged: finish released, then the stop observed, then the runtime
  disposed. The assertion failure stayed the primary failure.
- **P3 cleanup boundary:** the failure is raised at line 32, before the inner `try` at line 34. That cleanup therefore does not run,
  as documented. The probe added no markers for this.
- **Baselines:** the unmodified tests passed in the same copy, each with one passing line.

These controls show how the observation reports a failure. They say nothing about the cause of the original Windows failures.

## Natural recurrence 2026-11-04 (T020; bound by root monitoring)

This is the first natural failure of either test since the permanent observation was added.

**Where it ran**

- **Run:** 41836330209, `pull_request`, attempt 1, head `91192c5`.
- **Branch:** pull request #38. The delta is documentation only, so `src` and `tests` are identical to `main` `4ab04c4`.
- **Job:** 126596070216, `quality (windows-latest)`. The test step ended at 13:18:06Z.

**What failed**

- **Test:** `RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses`, at `RuntimeCleanupTests.cs:39`. This is the
  elapsed bound, which was line 29 before the observation was added.
- **Log:** `[xUnit.net 00:00:02.33] … [FAIL]`, then `Failed … [1 s]`.
- **Stack:** lines 39, 46, `StageObservation.cs:28`, 29 and 49.
- **Stage line:** `stage-observation T020 outcome=failed:Xunit.Sdk.TrueException cleanup-entry-wait-observed-ms=11.4
  stop-failure-observed-ms=1101.8`.
- **Raw log:** root's copy, SHA-256 `b929fff0f267e575b627fa3b9ffd93a61f7ab4c80a5638692f4b2017df3d7606`.

**The rest of the run**

- **Other suites:** UnitTests 452 of 452 passed. ContractTests had 1 failure out of 274. Linux quality in the same run passed
  274 of 274.
- **Other runs of this head:** the push quality run 41836319036 passed on Windows.
- **Job totals:** across the four runs of this head, 9 jobs succeeded, 1 failed and 1 was skipped. The skipped job is the
  dependent canonical consumer. The author's report had said "10 pass"; root's live job facts are authoritative.
- **Not retried.**

**What the line shows**

- **Within the bound:** the test resumed after the entry wait at 11.4 ms.
- **The overrun:** the stop failure was observed at 1101.8 ms. That is about 900 ms after the 200 ms deadline, and passing runs
  observe it at 200 to 216 ms.
- **What it does not show:** the line does not say whether the deadline callback, the stop's completion or the test's resumption
  was late.

## Source analysis 2026-11-04 (T020)

**Path at `91192c5`**

Source SHA-256:
- `RuntimeLifecycle.cs` `19c1437d…`;
- `RuntimeShutdown.cs` `069c219c…`;
- `RuntimeCleanupTests.cs` `bdb98c8e…`;
- `StageObservation.cs` `2d40b6aa…`.

1. `StopOnceAsync` creates the deadline as `new CancellationTokenSource(200 ms, host.Clock)`. The test passes no clock, so this
   is `TimeProvider.System`. The deadline is linked with the caller's token.
2. It moves to `Stopping`, stops operations, requests worker cancellation without waiting, and enters the stop gate.
3. `CompleteStopAsync` stops acceptance, cancels queued work, waits for the workers, then awaits
   `CleanupAsync().WaitAsync(token)`.
4. `CleanupCoreAsync` drains, disposes statistics, then disposes the store, which the test holds. The lease and `Stopped` come
   only after that.
5. The stop can therefore end only through the deadline. That `WaitAsync` faults, `StorageUnavailable` is thrown, and the gate
   is released in a `finally`.
6. While the store is held, the lease stays held.

The runtime has no `ConfigureAwait(false)`, so its continuations go to the caller's synchronization context.

**Points where the delay could arise**

- **(a)** before the deadline callback runs: the timer firing, or the thread pool dispatching it;
- **(b)** between that callback and the stop's completion: the continuation posted to the caller's context, or a stall of the
  thread that runs it;
- **(c)** between the stop's completion and the test resuming;
- **(d)** a stall of the whole process or host.

**Private controls**

- **Where they ran:** Linux, 8 cores, Release, in a private export of `91192c5` under `~/dev/.agenttemp`.
- **What was added:** one probe test file only. The export otherwise compared byte-equal with the archive, the clone stayed
  untouched, and nothing was kept in the source.
- **The probe:** it repeats the T020 scenario exactly and records:
  - D, when the selected 200 ms deadline callback starts. A wrapping `TimeProvider` passed through the public `clock` parameter
    records this.
  - C, when the stop task completes, through an `ExecuteSynchronously` continuation.
  - O, when the test observes the failure.
  - Two samplers: how far a dedicated thread overslept its 10 ms sleeps, and the dispatch latency of one pool item queued per
    wake.
- **Samples:** each case ran as its own test, so that it started in the same xUnit context as the canonical test. There were 5
  cases per condition.
- **Logs:** run 3, SHA-256 `1e960c51…`; the suite control, `05f8aa28…`. Both are kept privately.

| Condition (injected 900 ms) | D (ms) | C − D (ms) | O − C (ms) | pool dispatch max (ms) | dedicated oversleep max (ms) |
|---|---|---|---|---|---|
| Healthy | 197.3–202.0 | 0.7–0.9 | 0.0–0.1 | ≤ 3.2 | ≤ 3.2 |
| Deadline delivery delayed | 1098.6–1108.6 | 0.7–4.3 | 0.0–1.0 | ≤ 13.6 | ≤ 8.0 |
| Thread pool starved by blocking items | 632.3–902.4 | 0.6–1.5 | 0.0–0.1 | 622–1792 | ≤ 3.8 |
| Continuation posted to the caller's context delayed | 199.8–203.5 | 2498–2502 | 0.0–0.1 | ≤ 3.5 | ≤ 2.1 |
| Blocked after the stop completed | 200.7–203.4 | 0.7–1.2 | 900.2–900.3 | ≤ 2.1 | ≤ 2.6 |

- **Full-suite control:** one run of the ContractTests suite with the probe cases included passed 299 of 299. The canonical
  line read `3.0` and `202.3`, and the healthy probe cases read D 199.1–201.3, C − D 0.7–1.9 and O − C ≤ 0.1.
- **In every condition** the failure code was `StorageUnavailable`, the lease was held at observation, and the lease was
  released after cleanup.

**Measured facts, Linux only**

- The test's context is xUnit's `AsyncTestSyncContext` with no inner context.
- The deadline callback runs on a pool thread.
- The runtime's continuation after the deadline runs on a different, non-pool thread, about 1 ms later.
- The test's observation runs inline on that same thread, 0.0 to 0.1 ms after the stop completes, unless something blocks it.
- An earlier loop variant lost the xUnit context after the first sample. Its samples are not used.

**Inference, not measured on Windows**

- With nothing else registered on the stop task, point (c) is unlikely. The 1101.8 ms then lies at (a), (b) or (d).
- If the Windows path behaves like the Linux one, the current line cannot separate (a) a delayed timer from (a) pool
  starvation. The samplers and the D stamp can, and they also separate (b) and (c).
- (d) was not simulated. Its expected signature is that both samplers rise by the stall.
- No Windows cause is claimed.

**Observer effects of the candidate stamps**

- The wrapping clock moves the deadline from the `TimeProvider.System` fast path to `CreateTimer`, on the same timer queue.
- The sampler adds a dedicated thread that wakes every 10 ms, plus one tiny pool item per wake.
- A completion continuation (C) adds almost nothing beyond O here, and root's earlier decision excluded it.

**Decision for root**

There is no isolated defect, no Red and no fix proposal. The bounds, the assertions and the runtime are unchanged. Root chooses
between:

1. **Extend the T020 line, test-only:**
   - add the D stamp, with the clock wrapper selected only for the 200 ms deadline;
   - add the two sampler maxima, written in the existing `finally` after the window;
   - before any merge, run call-site controls like P1 to P4 and a healthy control, keeping the primary failure first and the
     cleanup order unchanged.
2. **Keep the line as it is** and wait for the next natural failure, accepting that (a), (b) and (d) stay inseparable.

Acceptance is as above.
