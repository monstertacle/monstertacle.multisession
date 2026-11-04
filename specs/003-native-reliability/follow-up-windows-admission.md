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
- A store stall and a thread-pool stall look identical to the caller. A 50 ms scheduling canary separates them: 3-7 ms of lag
  against 5983 ms.
- A stage-8 `Unknown` covers both "never written" and "durably accepted". Only the lookup separates them.
- A plain lock wait fails with `StorageUnavailable` and native code 5 after about 1 s, not with `AdmissionTimedOut`.

**Not established:** which of these happened on the Windows runner. No cause is claimed.

**Finding ADM-CERTAINTY-QUEUE-20261104**
- What: a commit that times out while still waiting in the store queue is reported as `Unknown` / `AdmissionCommit`, although
  the store holds `NotAccepted` / `StoreQueue` (control C2). This is conservative and safe; nothing is changed.
- Owner: this follow-up (Alexander Bleissem). Root decides.
- Next: root decides whether certainty preservation for queued commits is wanted. If so, it needs its own architecture analysis
  and a Red first.
- Acceptance: either a recorded decision to keep `Unknown`, or a reviewed change with a regression test. Deadlines and the
  no-replay rule stay unchanged in both cases.

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

Source/package/trace hashes and measured timings account for the observed expiry
or explicitly state the remaining uncertainty. Every timed-out candidate is looked
up without replay; no duplicate acceptance/send or invented completion is allowed.
Any isolated defect has a Red/Green regression and unchanged certainty/deadline
tests. Native default-profile observations and configured-Wave results are reported
separately. Open release blockers remain open in feature001; this follow-up is not
a substitute for a successful current Wave/native quality gate.
