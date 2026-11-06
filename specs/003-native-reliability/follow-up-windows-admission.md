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
