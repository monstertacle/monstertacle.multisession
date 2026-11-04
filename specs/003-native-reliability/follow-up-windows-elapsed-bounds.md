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
  - D, when the wrapped callback of the selected 200 ms deadline starts. A wrapping `TimeProvider`, passed through the public
    `clock` parameter, records this. That is a changed clock path, not the `TimeProvider.System` fast path of the canonical test.
  - C, when an `ExecuteSynchronously` continuation registered on the stop task runs. This is when that observer executed, not
    an atomic completion timestamp: inlining can be declined or deferred, and the observer can run before or after the
    awaited continuation.
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
- Under these controls only, the test's observation ran inline on that same thread, 0.0 to 0.1 ms after C, unless something
  blocked it. The control "Blocked after the stop completed" shows that a block there moves O by about 900 ms.
- An earlier loop variant lost the xUnit context after the first sample. Its samples are not used.

**Inference, not measured on Windows** (corrected 2026-11-04, NEC-T020-PROP-CLAIM-002 / DEF-T020-MEASURE-003)

- For the natural Windows failure, (a), (b), (c) and (d) all stay open.
  - The Linux near-inline result says nothing about Windows observer scheduling. It does not exclude (c), and it does not
    exclude (d), a process or host stall.
  - An earlier version of this record called (c) unlikely and placed the delay at (a), (b) or (d). That went beyond the
    evidence and is withdrawn.
- In the private controls, a delayed timer and pool starvation had different sampler and D signatures. Without C, D and the
  samplers do not separate (b) from (c): the interval after D stays one combined runtime-and-observer interval.
- (d) was not simulated. A high maximum does not prove a whole-host stall, and a low one does not exclude a short stall.
- No Windows cause is claimed.

**Observer effects of the candidate stamps**

- The wrapping clock moves the deadline from the `TimeProvider.System` fast path to `CreateTimer`, on the same timer queue.
- The sampler adds a dedicated thread that wakes every 10 ms, plus one tiny pool item per wake.
- A completion continuation (C) adds almost nothing beyond O here, and root's earlier decision excluded it.

**Decision for root** (superseded the same day by the review and root disposition below; kept as history)

There is no isolated defect, no Red and no fix proposal. The bounds, the assertions and the runtime are unchanged. Root chooses
between:

1. **Extend the T020 line, test-only:**
   - add the D stamp, with the clock wrapper selected only for the 200 ms deadline;
   - add the two sampler maxima, written in the existing `finally` after the window;
   - before any merge, run call-site controls like P1 to P4 and a healthy control, keeping the primary failure first and the
     cleanup order unchanged.
2. **Keep the line as it is** and wait for the next natural failure, accepting that (a), (b) and (d) stay inseparable.

Acceptance is as above.

## Proposal review and repair 2026-11-04 (T020)

**The findings, all under T020**

Two opposed reviews read the proposal above, and root decided. Nothing here is a product defect.

| Finding | What was wrong | Root disposition |
|---|---|---|
| NEC-T020-PROP-RESOURCE-001 / DEF-T020-MEASURE-002 | The private sampler joined without a bound and queued a pool item at every wake with no cap. Late callbacks still changed its maxima, so the window was not finite. In one starved sample the pool maximum, 1792.4 ms, included time after the observation at about 903.6 ms. | an accepted proposal-admission blocker |
| NEC-T020-PROP-CLAIM-002 / DEF-T020-MEASURE-003 | The Linux near-inline result was used to place the Windows delay. | an accepted docs repair, made in this record and in `tasks.md` |
| DEF-T020-MEASURE-001 | The clock wrapper changes the `TimeProvider.System` fast path. | keep System; the wrapper stays a historical private probe |

- **Root's decision:** a bounded, System-preserving, sampler-only private proposal.
- **Not readmitted:** D (the clock wrapper) and C (a completion observer).
- **History only:** the injected D and C controls above.

**Candidate R1 (private; historical, kept as reviewed; the canonical implementation below supersedes its metric wording)**

The candidate is a private export of `ba130ea`. Its diff from that head, SHA-256 `4303bc76…`, touches five test-side files:
- a new `SchedulingSampler` fixture;
- its tests;
- two read-after-window members on `StageObservation`, `Stamp(int)` and `Append(string)`;
- the ContractTests link;
- the T020 call.

The T020 call change is a single line pair:
- the old comment line becomes `SchedulingSampler.Started()`, before `began`;
- the existing `ObserveAsync` call gains a `collect` argument that appends the frozen snapshot.

Every original bound stays on its line: 200 ms at 31, 5 s at 32, 2 s at 36 and < 1 s at 39. The `finally` order is unchanged,
and nothing is added to the measured lambda.

**How the sampler works**

- **Thread:** one dedicated background thread wakes on a 10 ms monitor timeout and records how far it overslept. It exits on
  stop or at a 15 s lifetime cap.
- **Pool probe:** at most one is pending at a time. A wake that finds one pending queues nothing and counts a skip.
- **Window:** from `began` to the stop-failure-observed stamp, or to the freeze if that stage was not observed. Samples outside
  it never count.
- **Pending values:** a probe pending at the end is reported as a lower-bound age, never as a maximum. (R1 wording, too strong;
  corrected in "Canonical implementation 2026-11-04".)
- **Missing values:** an empty window reads `missing`, not 0.
- **Freeze:** it runs from `collect`, after the measured work and its cleanup.
  - The join is bounded at 200 ms.
  - The snapshot is immutable, and every later write is discarded.
  - It never throws. A fault becomes `sampler=fault:<type>` in the same line.
- **Not used:** reflection, xUnit internals, scheduler or context wrapping, and `ThreadPool` tuning.
- **Warnings:** the build ran with warnings as errors; there are no probe-only exceptions.

**Private controls (Linux, Release, capped build)**

| # | Control | Result |
|---|---|---|
| 1 | Healthy baseline: the candidate T020 test on the System path | passed; `cleanup-entry-wait-observed-ms=8.5 stop-failure-observed-ms=214.1 sampler=complete window-end=stop-failure-observed stop=joined dedicated-n=20 … pool-n=20 … pool-pending-at-end=false` |
| 2 | Held sampler thread | the freeze returned within the bound with `stop=timeout`; the late wake was discarded and the snapshot text was unchanged |
| 3 | Held pool probe | at most 1 outstanding; ≥ 3 skips; `pool-pending-at-end=true` with an age; the dispatch maximum read `missing` |
| 4 | Probe released after the freeze | discarded; the snapshot was equal and the same instance |
| 5 | Empty window (end = origin) | dedicated and pool maxima `missing`, counts 0 |
| 6 | Stop-failure stage not observed | `window-end=freeze` |
| 7 | Sampler fault with a failing measured window and a failing write | `AggregateException(primary, write)` with the primary first; the line read `sampler=fault:System.InvalidOperationException` |
| 8 | Variant: 1 s bound set to Zero | `TrueException` at line 39 stayed primary; cleanup markers showed release, stop, dispose and the lease disposed, then the sampler was frozen with `stop=joined` |
| 9 | Variant: entry never signalled | `TimeoutException` at line 32 stayed primary; both stages not observed; `window-end=freeze stop=joined` |
| 10 | Lifetime cap of 300 ms, never frozen | the thread ended by itself; `sampler=expired` |

- **Negative controls:** four one-line mutants were each detected:
  - an unbounded join failed control 2;
  - no pending cap failed control 3;
  - no window filter failed control 5;
  - no late discard failed controls 3 and 4.
- **Format:** `dotnet format --verify-no-changes` passed for both test projects.
- **Variants:** controls 8 and 9 were private edits, restored afterwards and checked by hash.

**What stays unknown**

- These are Linux mechanism controls, not a canonical qualification and not a Windows cause.
- The maxima are correlation envelopes and missingness for the window, not a classifier.
- Without D and C, (a) the timer cannot be separated from (b) or (c).
- (d) was not simulated.
- The observer effect is one thread waking every 10 ms, plus at most one tiny pool item, both during the timed window.

**Next (root decides; owner: this task's owner)**

1. Grant the candidate as a canonical test-only change, with its own review, the full local quality gate and the
   required native gates.
2. Or keep the current line.

Acceptance stays as above. T019, 008 T006 and 008 T007 (pull request #38 held) are not closed by this.

## Canonical implementation 2026-11-04 (T020)

**Admission**

Two narrow opposed reviews read the R1 candidate and its logs. Root then admitted a bounded canonical test-only implementation
of exactly the five candidate test files, after three metric corrections:

| Finding | Correction |
|---|---|
| NEC-T020-WAKE-AGE-001 (DEF-T020-REPAIR-METRIC-001 part 1) | The wake age is the time since the last selected wake, or since the origin when none was selected. It is not an oversleep and not a lower bound of one. A healthy age of a few ms says nothing about a late wait. |
| NEC-T020-LATENCY-WINDOW-002 (part 2) | A pool sample is selected by its run stamp inside the window, and its value is the full queue-to-run latency. The queue stamp can precede the origin, so a maximum is not an interval wholly inside the window. |
| DEF-T020-REPAIR-METRIC-001 part 3 | The pending fields become `pool-unresolved-for-window` and `pool-unresolved-age-in-window-ms`. They are a bookkeeping proxy for a probe queued at or before the end that was not recorded by the freeze, or that was recorded with a run stamp after the end. They are not proof that its callback had not started, and not a dispatch lower bound. The age is the end minus the later of the queue stamp and the origin. |

The R1 description above ("Pending values … lower-bound age") is kept as history and is superseded by this table.

**What changed from R1**

Only what the corrections needed:
- **Renames:**
  - the snapshot fields and output keys;
  - the internal outstanding-probe stamp;
  - the sampler thread name, now `scheduling-sampler`.
- **Label:** with no selected wake, the key is `dedicated-age-since-origin-ms` instead of `dedicated-last-wake-age-ms`.
- **Comments:** describing the window, the endpoint selection, the fallback and the observer effect.
- **One control hook:** `onProbeStart`, before the run stamp. It is null in the measured test.
- **Call-site comment:** neutral wording.

The algorithm, the caps, the bounded join, the immutable freeze, the late-write discard and the clock path are unchanged.

**The line in practice**

| Item | Value |
|---|---|
| R1 → R2 diff | SHA-256 `fe865f02…` |
| R2 export diff from `9e8349b` | `b1fff860…` |
| Canonical files | identical to the R2 export |
| Example line from the full local suite | `… stop-failure-observed-ms=201.8 sampler=complete window-end=stop-failure-observed stop=joined dedicated-n=19 dedicated-oversleep-max-ms=3.5 dedicated-last-wake-age-ms=3.7 pool-n=19 pool-dispatch-max-ms=2.4 pool-unresolved-for-window=false pool-skipped=0` |

**Unchanged in the T020 test**

- Only line 26 (the comment becomes `SchedulingSampler.Started()`) and line 48 (the `collect` argument) change.
- Lines 27 to 47 are byte-identical: `began`, the measured lambda, the 200 ms, 5 s, 2 s and < 1 s bounds and the `finally`.
- The deadline stays on `TimeProvider.System`.
- There is no C, no D, no wrapping and no `ThreadPool` tuning.

**Controls (Linux, Release, warnings as errors)**

- **New countercases:**
  - **Probe queued before the origin and run inside the window:** it is selected, and its full latency is at least the
    queue-to-release interval of about 100 ms. A clipped value would be about 50 ms.
  - **Probe started but not recorded by the freeze:** it is unresolved for the window, it is not counted, and its age is at
    most the window.
- **Adjusted controls:**
  - the empty window now carries `dedicated-age-since-origin-ms`;
  - a window with wakes carries `dedicated-last-wake-age-ms`.
- **Counts:** 9 sampler controls and the 5 existing `StageObservation` tests passed in both UnitTests builds (`net10.0` and
  `net10.0-windows`). Both `RuntimeCleanupTests` passed in the canonical clone.
- **Negative probes, each detected:**
  - latency clipped to the origin (about 50 ms against at least 102 ms);
  - a started but unrecorded probe treated as resolved;
  - the unresolved age not clipped (71 ms against a 20 ms window);
  - no origin-fallback label.
- **Historical R1 controls:** kept above, as reviewed.

**Full local quality, run once (Linux, capped, 14:26:49Z to 14:30:31Z)**

- **Restore, build and format:** locked restore, Release build with 0 warnings and 0 errors, and `format --verify-no-changes`.
- **Gate self-tests:** coverage-gate, package-hash and suite-inventory self-tests.
- **Suites:**
  - UnitTests 461 of 461;
  - ContractTests 274 of 274;
  - IntegrationTests 451 of 451;
  - PlatformTests 72 of 72;
  - Checkpoints.Tests 5 of 5;
  - Checkpoints.Delivery.Tests 99 of 99.
- **Audit:** no vulnerable packages reported.
- **Product coverage:** 97.74 % against the 87 % threshold.
- **Codemetr cover:**
  - passed and complete: 0 violations, 0 new, 0 worsened, 0 unreported;
  - line 96 %, branch 84.4 %, diff line 96.4 % and diff branch 87.6 %.
  - It also gave a tool warning, "1 file(s) have no coverage data", with an empty `unreported` list. That is recorded as
    observed and not resolved here.

**What stays unknown**

- The Windows cause: (a) to (d) are open.
- Whether the sampler's observer effect shifts a natural failure.
- What the maxima can show: they are correlation envelopes, not a classifier.
- No native run was made.

**Next (root)**

- Review the exact patch, then decide on upload and the required native gates.
- Owner: this task's owner.
- Acceptance: unchanged.

T019, 008 T006 and 008 T007 (pull request #38 held, alpha.10 unpublished) are not closed.
