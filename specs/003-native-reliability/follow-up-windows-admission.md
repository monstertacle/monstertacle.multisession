# Native default-budget admission variability

Owner: Alexander Bleissem
Status: in progress; source analysis and private controls recorded 2026-11-04 (evidence/windows-default-admission-source-analysis-20261104.md), measurement proposal awaiting root; attribution added and envelope measured (evidence/windows-restart-admission-20261102.md, evidence/native-envelope-20261103.md); first attributed default-budget timeout at AdmissionLookup, NotAccepted (evidence/windows-capacity-admission-20261103.md); no production latency or root-cause claim accepted; Windows 11 release-matrix gap open
Origin: PR run41054347449 at head98948da, source f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490;
routing/deterministic AdmissionTimedOut at AdmissionCommit with Unknown outcome.
Related release work: feature001 T099 and its required real native host evidence.
The failed runner's original stores were not retained in standard artifacts;
their later candidate outcomes were not captured. Future controlled probes must
retain their owned state for lookup and cannot reconstruct that missing evidence.

## Bounded scope and next steps

1. Reproduce the default five-second public admission workload on actual supported
   Windows hosts and a Linux control using one exact package set. Use fresh private
   stores and a controlled provider; never resubmit an uncertain candidate.
2. Put measurement infrastructure outside the repository under
   ~/dev/e2e-harness-bundles. Record per-operation elapsed time, queue/store activity
   and appropriate native I/O evidence to distinguish waiting from transaction or
   commit latency. The existing stage alone cannot make that distinction.
3. Preserve each failure, candidate ID, later lookup outcome, raw trace provenance,
   runtime/SQLite/OS versions and fixture pressure. Compare instrumented and normal
   execution; do not infer a runner problem or a product race from variability.
4. Change runtime code only after an actual defect is isolated and a deterministic
   regression demonstrates it. Otherwise document the measured operating envelope
   and obtain the responsible performance/release decision for the default profile.

Dependencies: native Windows Server and required Windows11 host access for the
release matrix; offline controlled workloads need no provider credentials. The
Wave consumer's explicit thirty-second budget is a separate acceptance profile.

## Recurrence 2026-11-04 (pull request #30)

- Run: push quality run 41769537053, attempt 1, `windows-latest`, head `9a76a2f`. That head adds only documentation on
  top of `main` `6b2dfbf`.
- Failure: `IntegrationTests/RuntimeLifecycleTests.StopsAcceptanceAndFinalizesAllQueuedPages` threw
  `SessionOperationException: AdmissionTimedOut` from `RequestAdmission.SubmitAsync` at `RuntimeLifecycleTests.cs:24`.
- Where: in the setup loop that queues 102 requests under the default admission budget, before the stop window this test
  measures. It is not T019 or T020. Those are different tests and bounds.
- Same head: the pull_request quality run 41769544991 passed. Neither run was retried.
- Not in the log: the loop index, the failure stage, the outcome of the candidate and any native code. Like the earlier
  records, it adds no cause. It is bound here as a further default-budget recurrence.

## Source analysis and private controls 2026-11-04 (evidence/windows-default-admission-source-analysis-20261104.md)

Source state: `main` `18cbcbba`. The record contains:
- the submit architecture: the budget clock, the store worker, the work-item states, durability and the error mapping;
- the TRX comparison of 41769537053 with 41769544991;
- five private offline Linux controls, plus an envelope of the exact failing workload.

**Established by the controls**
- The public error keeps only one bit of stage information.
- A store stall and a thread-pool stall look identical to the caller. In these controls a 50 ms scheduling canary separated
  them: 3-7 ms of lag against 5983 ms. A low, zero or missing sampled lag is not proof that no scheduling stall happened.
- A stage-8 `Unknown` covers both "never written" and "durably accepted". Only the lookup separates them.
- In the Linux control, a plain lock wait failed with `StorageUnavailable` and native code 5 after about 1 s, not with
  `AdmissionTimedOut`. Windows VFS sharing retries are not covered.

**Not established:** which of these happened on the Windows runner. No cause is claimed.

**Finding ADM-CERTAINTY-QUEUE-20261104**
- What: a commit that times out while still waiting in the store queue is reported as `Unknown` / `AdmissionCommit`, although
  by source the store completes a still-waiting item with `NotAccepted` / `StoreQueue` (control C2; the transition itself was not
  logged). This is conservative and safe; nothing is changed.
- Owner: this follow-up (Alexander Bleissem). Root decides.
- Next: root decides whether certainty preservation for queued commits is wanted. If so, it needs its own architecture analysis
  and a Red first.
- Acceptance: either a recorded decision to keep `Unknown`, or a reviewed change with a regression test. Deadlines and the
  no-replay rule stay unchanged in both cases.
- Root decision 2026-11-04: KEEP the conservative `Unknown` / `AdmissionCommit`.
  - Reason: the caller's deadline can win before the store's no-write proof arrives, and a durable acceptance stays possible
    (C3). The finite deadline and the no-replay rule are kept.
  - Accepted cost: some requests that never ran need an extra lookup, and may stay unknown if the store is unavailable. The
    queue-stage attribution is lost. This is a conservative diagnostic burden, not a demonstrated correctness bug.
  - This closes the precision decision only. No runtime defect is fixed, and the Windows cause stays open.

**Measurement proposal**, for root decision (section 7 of the record, nothing implemented):
- M1: test-only failure attribution: loop index, stage, status, recent durations and the candidate lookup.
- M2: a test-only scheduling canary.
- M3: internal store stage stamps; this needs an architecture decision.
- M4: Windows journal I/O attribution.
- Recommended: M1 and M2 as one bounded test-only change.
- Owner: Alexander Bleissem, with root deciding.
- Dependencies: a root measurement or code grant.
- Acceptance: a future natural failure reports its stage, index, candidate outcome and canary lag, with every bound unchanged and
  no reruns started to find one.
- Root decision 2026-11-04: M1 and M2 are granted as a test-only change in
  `RuntimeLifecycleTests.StopsAcceptanceAndFinalizesAllQueuedPages` (the 102-submit setup loop) only.
  - Conditions: M1-SCOPE, M1-PRIMARY, M1-TIMING, M1-LOOKUP, M2-SAMPLES, M2-POINTS, M2-LIFETIME, M1M2-VALIDATION and
    SCOPE-BOUNDARY.
  - The original exception is rethrown unchanged; the lookup is finite and read-only; the canary reports readiness, sample count
    and the end gap.
  - No runtime, API, schema, pipeline or budget change. M3 and M4 are not granted.
  - Owner: Alexander Bleissem. Next: a separate test topic.
  - Implemented 2026-11-04 on branch `test/003-admission-setup-diagnostics`
    (evidence/admission-setup-diagnostics-architecture-20261104.md). Test-only.
    - New fixtures: `AdmissionSetupObservation`, `SchedulingSampler`, `CandidateLookup` and `TaskObservation`.
    - Only the first setup loop uses them.
    - On a failed submit:
      - a synchronous snapshot is taken first: index, candidate, the exception's own fields, submit and setup time, the last 10
        durations, thread-pool counters, the GC pause delta and the sampler state;
      - then the sampler is stopped within a bound;
      - then one read-only lookup runs, with a 2 s token and a 3 s outer bound;
      - one line is written and the original exception is rethrown unchanged.
    - A failing report is thrown with the primary failure first.
  - Red: 10 of 11 new tests failed against behaviourless signatures. A first scaffold that did not compile (CA1822) is not
    counted. Green: 11 of 11.
  - Private call-site controls, run in a patched private copy that is not retained:
    - the unchanged old loop under a store stall writes nothing;
    - the new loop writes one line under a store stall (stage, index 50, candidate, low sampled lag);
    - under a scheduling stall it shows a 5965 ms lag;
    - under a lookup stall it shows `lookup=timeout` after 3003 ms;
    - under an output failure it gives `AggregateException`, primary first;
    - the passing run writes nothing and leaves no state behind.
  - Mutations, all detected by assertions:
    - the primary is wrapped;
    - the lookup or the sampler stop is unbounded;
    - missing samples are reported as zero;
    - absence is reported as not accepted;
    - an output failure replaces the primary;
    - the ring is unbounded.
  - These controls show how the diagnostic reports a failure. They say nothing about the cause on Windows.
  - Review fixes 2026-11-04. Root decided fix-first after two opposed reviews of `61da49d`. Both races were found by review
    from the source, not observed in a run.
    - NEC-M1M2-LOOKUP-001 (DEF-S33-001): a lookup that faulted after the outer wait timed out was not observed.
      `CandidateLookup` now observes the pending task on every outer timeout, whatever its state. A lookup's own timeout is
      still reported as an error. Red/Green uses an injected wait that faults the lookup and then times out.
    - NEC-M1M2-SNAPSHOT-002 (DEF-S33-002): a sample published after the failure point could produce a negative end gap.
      The sampler now reads its own observation time under the lock that publishes samples. The line reports how long after
      the failure point the sampler was read and whether the last sample came after it. The failure point and the submit timing
      are unchanged.
    - NEC-ADM-M1-WORDING-002 (DEF-ADM-R2-WORDING-01): the section 7 sentence of the source analysis now names the separate test
      diagnostic line and the unchanged rethrown exception.
    - The native scenario state-root question is registered as T021 (follow-up-native-scenario-root-ownership.md). It is open
      and not part of these diagnostics.

## Unbound Windows restart failures 2026-11-02 (run 41505132813; bound 2026-11-06)

Bound by root forwarding (intake MS-RECOVERY-RUN-41505132813-UNBOUND, 2026-11-06), from the raw job log (SHA-256 `8160ef7f2b6a6144…`) and
the retained `quality-windows` artifact (IntegrationTests TRX SHA-256 `eab93e76c290a098…`). Nothing was rerun.

**Where it ran**

- **Run:** 41505132813, `push`, attempt 1, branch `feature/015-continuity-pinning`, head `6df1707`. That head is an ancestor of
  `main`.
- **Job:** 125482363143, `quality (windows-latest)`, Microsoft Windows Server 2025, runner 2.338.2.
- **Suite totals:** IntegrationTests 429 of 432 passed; UnitTests and ContractTests passed.

**The three failures (raw)**

- **`ValidationScenarioTests.RecoveryExecutesTenNativeRestarts`:** started 19:01:48Z, took 1 min 34 s. The `recovery` scenario
  exited 2 with `ScenarioExecutionFailed`, generic code 16, certainty 0, no exception type, no site, no cleanup failure. The
  stack shows `ValidationScenarioTests.cs:84`, 61 and 64.
- **`PersistenceRecoveryTests.PreservesExactStateAcrossForcedRestart(iteration 2)`:** started 19:03:23Z, took 35.0 s, and
  **`(iteration 0)`** started 19:03:58Z and took 34.6 s. Both threw `System.TimeoutException` at `CrashHarness.cs:40` (and 49),
  from `PersistenceRecoveryTests.cs:33` (and 77). At that head, line 40 is the 30 s wait for the crash worker's first output
  line: the worker wrote no line within 30 s.

**Measured around the failures (same TRX)**

- Iteration 8 started at 19:04:32Z and passed, but took 22.0 s.
- Every later iteration (19:04:54Z onward) passed in about 1.4 s.
- The failures fall inside one window of slow tests, 19:01:48Z to about 19:04:54Z. What made it slow was not measured.

**Why this is not bound as a recurrence**

- The recorded forced-restart failures (evidence/windows-restart-admission-20261102.md, run 41533898656) failed when the
  crash worker *reported* `AdmissionTimedOut` at its barrier.
- Here the worker wrote **no** line, so neither the admission nor the stage is known.
- The slow-window pattern is similar, but the failure signature differs, so no common cause is claimed. Separately, the
  `recovery` scenario's generic code 16 matches the shape of the original T016 capacity failure. No shared cause is assumed
  there either.

**What current `main` would show**

- **The `recovery` scenario:** untyped scenario failures name their exception type and throw site since `c2c498b`
  (2026-11-03, after `6df1707`).
- **The crash worker:** failures it *reports* carry its step, stage and candidate since `26286be`.
- **Gap:** a first-line timeout still throws a bare `TimeoutException`, and the worker's redirected standard error is not
  read.

**Next executable steps**

1. Root monitoring binds the next natural Windows recurrence of either test on `main`; a `recovery` recurrence now carries
   its type and site.
2. T023 (proposed) closes the first-line timeout gap, test-only. No rerun, no bound change, no runtime change before a
   proven defect.

## Acceptance


Source/package/trace hashes and measured timings account for the observed expiry
or explicitly state the remaining uncertainty. Every timed-out candidate is looked
up without replay; no duplicate acceptance/send or invented completion is allowed.
Any isolated defect has a Red/Green regression and unchanged certainty/deadline
tests. Native default-profile observations and configured-Wave results are reported
separately. Open release blockers remain open in feature001; this follow-up is not
a substitute for a successful current Wave/native quality gate.

## T023: source analysis before implementation (2026-11-06)

Source `77e39e6`. Root approved T023 test-only on 2026-11-06T14:01:51Z.

**Current behaviour** (`tests/Monstertacle.Multisession.IntegrationTests/Fixtures/CrashHarness.cs`, `StartAsync`)

- The harness starts the crash worker with standard output and standard error redirected.
- It waits up to 30 s (`_processLimit`) for the first output line. A timeout throws a bare `TimeoutException`; the existing
  catch disposes the harness, which ends its own child.
- **Standard error is never read.** Its content is lost, and a worker that writes more than a pipe buffer to it would block
  (a latent deadlock).
- The worker (`examples/Monstertacle.Multisession.ValidationHost/CrashWorker.cs`) records its steps in memory and reports them
  only inside its first line. So on a first-line timeout no step is known.

**Decision (test-only; 30 s limit, barrier check, candidate lookup and process ownership unchanged)**

1. **Read the worker's standard error from its start** into a bounded tail, so a full pipe cannot block the worker.
2. **On a first-line timeout:** end only the harness's own child (the existing `KillAsync`, by its captured process), then
   throw a `TimeoutException` with the original as its inner exception. The message names the limit, the elapsed time, that
   no worker step is known (no line), and the bounded standard-error tail.
3. **Owned controls:** a validation-host fixture worker mode, beside the existing crash, disk-full and ownership workers, that
   writes nothing to standard output and is ended by the harness:
   - `silent`: writes nothing;
   - `stderr`: writes far more than a pipe buffer to standard error, ending with a marker.

   Both run with a short control limit; the production limit is unchanged.

**Limits**

- The report says which step was running only if the worker wrote it to standard error.
- The cause of a silent worker is not shown.

## T023: implementation (2026-11-06)

Candidate commits on `test/003-t022-t023-diagnostics`: red `1bad070`, green `f9caaa6`. Test and fixture code only; the
validation host gains a fixture worker mode.

**Fixture worker**
- `examples/Monstertacle.Multisession.ValidationHost/FirstLineFixtureWorker.cs`, with its protocol constants in
  `FirstLineFixtureProtocol.cs` (linked into the integration tests, like `CrashProtocol.cs`).
- `--first-line-fixture-worker silent|stderr`: it writes its process id to `first-line-fixture.pid` in its working directory
  and never writes standard output.
- `stderr` writes 2,048 lines of 63 characters (131,072 bytes, more than a pipe buffer) to standard error, then the marker.
- It ends by itself after 2 minutes.

**Harness** (`tests/Monstertacle.Multisession.IntegrationTests/Fixtures/CrashHarness.cs`)
- `StartAsync` is unchanged for its callers. It calls the new `StartWorkerAsync(start, point, limit)` with the unchanged 30 s
  limit.
- Standard error is read from the start into `BoundedTextTail` (the last 2,048 characters and a count of every character).
- **On a first-line timeout:**
  - the harness ends only its own child, by its captured process (the existing `KillAsync`);
  - it then throws a `TimeoutException` whose inner exception is the original one;
  - the message names the limit, the elapsed time, that no worker step is known (steps are reported only in the first line),
    the child's process id, and the standard-error tail (or `standard error: none`).
- The barrier check, the candidate lookup and the process ownership are unchanged.

**A second defect, found by the red: harness cleanup replaced the timeout.**
- The red run showed both controls failing with a `FileNotFoundException`, not a timeout. The harness's own cleanup waited
  for the release of `multisession.db`, a file the worker had not created.
- Any worker that times out before it creates its store therefore lost the timeout.
- Fixes:
  - an absent store has nothing to release;
  - a failure to end the child is reported after the start failure, in an `AggregateException`, never instead of it.

**Controls** (`CrashHarnessTimeoutTests`, 2 s control limit)

| Control | Red (`1bad070`) | Green (`f9caaa6`) |
|---|---|---|
| silent | `FileNotFoundException` replaced the timeout | `TimeoutException` (inner `TimeoutException`), reported elapsed between the limit and the measured time, `standard error: none`, the recorded process id named and no longer running |
| stderr only | the same | the same, plus the marker, the exact count of 131,102 characters, a message under 8 KiB |

- The existing `PersistenceRecoveryTests`, `CrashBoundaryTests`, `CrashJournalTests` and `CrashWorkerDiagnosticTests` pass
  on both target frameworks: 23 of 23 each.

**Mutants** (each restored; built with `--no-incremental`)

| Mutant | Result |
|---|---|
| t1, standard error not read | the stderr control fails: no marker, so the worker blocked on a full pipe |
| t2, unattributed timeout | both controls fail |
| t3, absent store fails cleanup | both controls fail |
| t4, a 1,000 times larger tail | the stderr control fails |
| t5, the tail described before the child is ended | **survived** |
| t6, inner exception dropped | both controls fail |
| t7, elapsed time measured from earlier | both controls fail |

- **t5 survived.** The child had written all of its standard error long before the 2 s limit, so in these controls the order
  is not observable.
- **Invalid runs, kept as evidence:** the first t2 and t4 variants did not compile, because of the unused-variable and
  unused-parameter analyzers.

**Limits**
- The step is known only if the worker wrote it to standard error. The cause of a silent worker is not shown.
- The tail is reported verbatim, bounded to 2,048 characters. The existing check against a state path or payload in the
  message covers the store-failure line, not this timeout message.
- No Windows run was made: `ProcessExitWait`'s Windows branch and the Windows file-release wait were not exercised.

### T023 revision after the codemetr guard (2026-11-06)

**Guard finding and fix (`dd8483d`)**
- The full guard (`dotnet tool run codemetr-csharp -- check --format json`) on `f9caaa6` found one new violation: class
  coupling of `CrashHarness` was 43, over the limit of 40.
- No baseline, exclusion or limit change. The timed first-line read, the standard-error tail and the timeout attribution
  moved into `tests/Monstertacle.Multisession.IntegrationTests/Fixtures/FirstLineWait.cs`.
- Behavior is unchanged. `StartWorkerAsync` constructs the wait before the child starts, which subscribes standard error. It
  calls `ReadAsync(harness.KillAsync)` after the start.
- The guard then passes with 0 violations.

**New control (`51f2c42`):** `FailureToEndTheChildIsReportedAfterTheTimeout`. The control's own ending step kills its child
and then throws. The result is an `AggregateException`: [0] the attributed `TimeoutException`, which says the process "could
not be ended", and [1] the owned failure.

**Mutants on the final shape (r2)**

| Mutant | Result |
|---|---|
| t1, standard error not read | the stderr control fails |
| t2, unattributed timeout | both controls fail |
| t3, absent store fails cleanup | both controls fail |
| t4, a 1,000 times larger tail | the stderr control fails |
| t5, the tail described before the child is ended | **survived** (as before) |
| t6, inner exception dropped | both controls fail |
| t7, elapsed time measured from earlier | both controls fail |
| t8, the harness's outer catch lets a disposal failure replace the start failure | **survived** |
| t9, a failure to end the child replaces the timeout | the new control fails |

**t8 survived; this path is not covered.** The aggregation in `StartWorkerAsync`'s outer catch (a start failure kept when
the later disposal fails) is not exercised by any control. With the absent-store fix, no owned control makes that disposal
fail without a further seam. It is defensive; root decides whether it stays, gets a seam, or is removed.

## T023 opposed-review fixes: analysis before implementation (2026-11-06)

Source `76b0e19`. Two opposed reviews (operations record owner-sdk-t022-t023-opposed-review-20261106, status quo and
necessity) reviewed it, and root accepted two findings at 2026-11-06T15:00:03Z. T022 and the two earlier T023 fixes are
retained unchanged.

**SDK-SQ-001 (P1; alias SDK-NECESSITY-STDERR-MEMORY-002): bounded reading, not only a bounded message**
- **Current defect:**
  - `FirstLineWait` uses `BeginErrorReadLine`/`ErrorDataReceived`. The process class accumulates each line until its
    terminator or the end of the stream, so an unterminated line is held whole.
  - `BoundedTextTail.Append` then appends the whole line to a `StringBuilder` before trimming it.
  - The 2,048-character cap bounded the final message, not the reader or the tail storage.
- **Decision:**
  - From the start, a drain task reads `StandardError` with `ReadAsync` into one fixed character buffer (no `ReadLine`,
    `ReadToEnd` or line buffer).
  - Each chunk goes into a fixed character ring of 2,048 that keeps the last characters. It counts every decoded character,
    line terminators included as decoded.
  - The drain runs until the end of the stream, so the pipe never blocks the child.
  - On a timeout, the harness ends its own child first. It then waits, bounded, for the drain to reach the end of the
    stream, so the tail holds what the child wrote. If the drain does not finish in time, the tail is reported as read so
    far.
  - The drain ends by itself at the end of the stream or when the stream is closed with the disposed process. Its stream
    errors are observed, never left unobserved.
- **Control (red first):** an owned worker writes 8 MiB of standard error without any line terminator, then the marker.
  - The tail must keep exactly the last 2,048 characters, with the exact count.
  - The tail's stored characters must never exceed 2,048, measured as a peak.
  - Allocation during the timed wait must stay far below the stream size (process-wide allocated bytes, a test-only
    measure with a stated margin).

**SDK-SQ-002 (P2; closes SDK-T023-OUTER-CATCH-UNCOVERED-003): cleanup of a never-started child, and the handle**
- **Current defects:**
  - A start failure (for example a missing executable) reaches the outer catch. `DisposeAsync` then calls `KillAsync`,
    whose `HasExited` throws on a never-started `Process`, so an `AggregateException` replaces the plain start failure.
  - `DisposeAsync` awaits `KillAsync` before `process.Dispose()`, so any failure there skips disposing the handle.
- **Decision:**
  - A child that never started is not ended: the outer catch disposes its `Process` and rethrows the start failure
    unchanged. There is nothing to signal.
  - `DisposeAsync` disposes the `Process` in a `finally`, after `KillAsync`, whatever `KillAsync` does.
  - The outer aggregation for a started child stays: a start-path failure first, then the cleanup failure.
- **Controls (red first):**
  - a private path with no executable through the real `StartWorkerAsync` keeps the original start exception;
  - a harness whose own started child's cleanup fails still disposes its `Process` (observed through the component's
    `Disposed` event). The cleanup failure is an unreadable own store file in the owned directory; the child is killed
    first, by its captured process only.

**Unchanged:** the 30 s production limit; the typed, attributed primary timeout; only the captured child is ended; no
runtime source change.

### T023 opposed-review fixes: implementation (2026-11-06)

Candidate commits on `test/003-t023-bounded-reader-start-cleanup` from `76b0e19`: analysis `b6b5bc1`, red `d060532`, fix
`b2e86ad`, controls hardened `a86b608`. Test and fixture code only; no runtime source change.

**SDK-SQ-001: bounded reading**
- `FirstLineWait` starts one drain task right after the child starts: `StandardError.ReadAsync` into one fixed
  4,096-character buffer, until the end of the stream. There is no line reader.
- `BoundedTextTail` is one fixed ring of 2,048 characters. It counts every decoded character, line terminators as decoded.
- On a timeout the own child is ended first. The drain is then awaited for at most 5 s; if it has not finished, the message
  says so and reports the tail as read so far.
- **Control** (`CrashHarnessStderrMemoryTests`, in its own non-parallel collection because it reads the process-wide
  allocation counter): an owned worker writes 8 Mi characters of a cyclic pattern and the marker, with no line terminator.

| | `76b0e19` (red) | Fix |
|---|---|---|
| Tail peak | 8,388,638 characters | 2,048 |
| Allocation during the wait | about 71 MB | about 4.4 MB (bound 8 MiB) |
| Count | wrong | exact (8,388,637) |
| Last 2,048 characters | — | exact |

**SDK-SQ-002: never-started cleanup and the handle**
- If `Start` throws or returns false, the outer catch disposes the `Process` and rethrows the start failure unchanged.
- `DisposeAsync` disposes the `Process` in a `finally` after `KillAsync`. The started-child aggregation (start-path failure
  first) is unchanged.
- **Controls:**
  - a private path with no executable keeps its `Win32Exception`; it was an `AggregateException` on `76b0e19`;
  - the own child's cleanup fails on an unreadable own store file after the child was killed. The failure propagates, the
    child is gone, and the `Process` is disposed: it no longer has an associated process; on `76b0e19` it was not
    disposed.
  - Linux only, because the failure uses a Unix file mode.

**Mutants** (each restored; non-incremental builds): 8 of 10 red.

| Survivor | Why it is not observable here |
|---|---|
| r04, no bounded drain wait after the kill | The owned workers finish writing long before the limit |
| r10, the never-started branch does not dispose | That `Process` is internal to `StartWorkerAsync` |

**Limits**
- The allocation control is a test-only, process-wide measure.
- `StreamReader` and pipe internals keep their own fixed buffers.
- No Windows run.
- T022 and the earlier T023 fixes are unchanged.

### T023 residual SDK-SQ-R1-001: drain outcome (analysis before implementation, 2026-11-06)

The narrow recheck (operations record owner-sdk-t023-bounded-reader-current-recheck-20261106) accepted the bounded reader
(SDK-SQ-001) and the start and disposal fixes (SDK-SQ-002). Root accepted one P2 residual at 2026-11-06T15:54:13Z.

**Defect.**
- `FirstLineWait.DrainAsync` catches every `IOException` and `ObjectDisposedException` and returns a completed task.
- So a read failure before the end of the stream looks the same as a clean end. A child whose standard error could not be
  read is reported as `standard error: none`, or with a tail that looks complete. That is a silently swallowed error
  (constitution: errors handled traceably).

**Decision (smallest bounded change).**
- The drain keeps one outcome: the end of the stream, or the type name of the exception that ended the reading. No message,
  stack or payload is kept.
- Every exception that ends the drain is caught and recorded, so it never replaces the attributed timeout and never goes
  unobserved.
- The attributed timeout report adds ` (standard error incomplete: reading failed with <type>)` when the drain ended by a
  failure before the report. The existing "did not end within 5 s" note stays.
- **When a failure counts as unexpected:** by timing, not by exception type. The report is built after the own child was
  ended and before the harness disposes the `Process`. So a failure the report sees happened before any disposal and is
  unexpected, whatever its type.
- **Expected disposal stays benign.** A stream closed by the harness's own disposal ends the drain only after the report, so
  it changes nothing that is reported; the drain still observes it.
- **Unchanged:** a clean end of the stream, the fixed 4,096-character chunk and the 2,048-character ring, the attributed
  timeout first, and a cleanup failure after it.

**Control (red first).** An owned fixture child whose standard-error reader is closed by the test before the drain starts,
so reading fails before the end of the stream. On `00df079` the report says `standard error: none` as if clean; it must
instead carry the incomplete indicator. The silent worker's clean end must still report `standard error: none` without the
indicator.

**Implementation** (red `376829c`, fix `cdf9e8c`):
- `FirstLineWait` keeps the drain's ending as the exception type name only, and the timeout report appends
  ` (standard error incomplete: reading failed with <type>)`.
- **Control `UnreadableStderrIsReportedIncompleteNotNone`:** on `00df079` the report had no indicator; with the fix it reports
  `ObjectDisposedException` as incomplete. The silent worker's clean end still says `standard error: none` with no indicator.
  The bounded reader, the start and disposal controls and the 8 Mi unterminated control are unchanged and green.
- **Targeted mutants:** both red. One drops the indicator; the other classifies `ObjectDisposedException` as expected
  disposal by its type, which would hide a closure before the report.

### T023 fixture worker coverage (SDK-T023-FIXTURE-WORKER-CRAP-001; analysis before the change, 2026-11-06)

**Finding.** Root's publication-readiness review (operations record
owner-sdk-qualified-diagnostics-publication-readiness-20261106) found the coverage gate failing on the fixture worker.
- The coverage gate in `Invoke-Quality` (`codemetr-csharp-cover check`) fails on
  `FirstLineFixtureWorker.RunAsync(string[])`: CRAP 110 against a limit of 30.
- The cause: complexity 10 and 0% in-process coverage. The worker runs only as a child process of the integration controls,
  so no in-process test reaches it.
- The structural codemetr check passed throughout; it does not measure coverage.

**Decision (test-only).**
- **Seam:** the worker keeps its process entry `RunAsync(args)`. It delegates to an internal overload that receives the
  error writer, the working directory and the self limit; the entry passes `Console.Error`, the current directory and the
  2-minute limit. The child process therefore behaves byte for byte as before, and the owned child controls (silent, stderr,
  8 Mi unterminated) keep exercising that path.
- **Tests:** in-process tests in `Monstertacle.Multisession.UnitTests`, which already sees the validation host's internals,
  call the overload with their own writer, a private directory and a short limit, and assert the observable outcomes:
  - a refused argument list exits 2 at once, with no effect;
  - `silent` records the process id, writes nothing, is still running before its limit and then exits 3;
  - `stderr` writes exactly its lines, then the marker;
  - `stderr-unterminated` writes the cyclic pattern for exactly its count, across the shorter last chunk, then the marker,
    with no line terminator.
- **Not done:** no global console redirection and no two-minute wait. No exclusion, filter, baseline, threshold or scope
  change, and no split made only to lower the score.

### T023 subtask SDK004: the real entry dispatch arm (analysis before the test, 2026-11-06)

**Diagnosis.** The read-only diagnosis (operations record owner-sdk004-diff-measurement-readonly-diagnosis-20261106) was
accepted by root at 2026-11-06T17:27:04Z. Against the actual `origin/main` base, the changed-line coverage counts two kinds of
line:
- **The constant lines** of `FirstLineFixtureProtocol.cs`: a tool false positive (no IL, no sequence points), owned
  separately. Not changed here.
- **`Program.cs` line 16:** the real dispatch arm `FirstLineFixtureProtocol.WorkerArgument => FirstLineFixtureWorker.RunAsync`,
  executed only in child processes that are killed, so its hits are lost.

Root keeps the actual-base gate; the configured historical base is not proof.

**Decision (test only).**
- One in-process control in `Monstertacle.Multisession.PlatformTests`, in the existing `WorkerConsoleIsolation` collection
  (which already owns console redirection).
- It invokes the validation host's real `Assembly.EntryPoint`, the compiler's synchronous wrapper of `async Task<int> Main`,
  with the first-line worker argument and an unknown variant.
- It asserts exit 2, no standard output or standard error, and no process-id record.
- **What it distinguishes:** a dropped arm falls through to the scenario runner, which writes a report, so the test fails.
- **What it cannot distinguish:** a misroute to another fixture worker. All of them refuse a two-argument list silently with
  exit 2, and a valid variant would block for its two-minute limit.
- **Unchanged:** `Main`, the constants and the configuration.
