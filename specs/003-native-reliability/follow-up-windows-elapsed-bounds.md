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
