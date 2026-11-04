# Tasks
Created: 2026-10-30T06:20:36Z
- [x] T001 Read source, CI failures, governance and current SharpClaw; record evidence before implementation.
- [x] T002 Specify behavior, architecture, contracts and acceptance.
- [x] T003 Reproduce scheduling hypothesis outside repository and record measured result.
- [x] T004 Add executable scheduler regression and archive observed behavioral Red.
- [x] T005 Implement independently schedulable owned executor, preserving bounded FIFO/cancellation/disposal.
- [x] T006 Add real safe-diagnostic/propagation/timeout regressions; archive Red.
- [x] T007 Implement structured diagnostic normalization without changing acceptance certainty.
- [x] T008 Test and fix validation report error preservation and sanitization.
- [x] T009 Run full static metrics, native quality/Cover and package consumption.
- [x] T010 Repeat native Windows scenarios and inspect safe diagnostics for any recurrence.
- [x] T011 Commit/push reviewable work, bind native CI evidence, disposition all findings.
- [x] T012 Correct UnitTests host target selection after observed Windows foreign-assembly coverage failure; preserve inventory strictness and rerun native CI.
- [x] T013 Measure the complete validation host in Cover, preserve the separate native product gate, test real native scenario ownership and close measured coverage gaps; see host-coverage.md.
- [x] T014 Apply reviewed Wave-only admission profile after observed native default-budget timeout, preserving SDK defaults and every functional assertion; require fresh native CI.
- [x] T015 Close current-head Windows CRAP gaps with deterministic direct worker protocol tests in WorkerProtocolTests.cs and repeat complete native CI without gate changes.
- [ ] T016 Investigate the independent published Windows capacity failure under follow-up-capacity-probe.md; preserve bounds and original failure, no retry-as-fix claim.
- [x] T017 Disposal after a reported stop failure (follow-up-dispose-stop-race.md): contract Q implemented with a controlled Red then Green, own-failure and shared-disposal controls and mutations on fix/dispose-stop-failure-ownership; stays open until review, merge and native gates on the merged head. Its item C1 (a stop still unwinding while disposal completes ends with a raw ObjectDisposedException, reproduced and pre-existing) is open with its own owner, task and acceptance and is not closed by T017.
- [x] T018 C1 (follow-up-dispose-stop-race.md): keep the private stop gate usable instead of disposing it, per the root decision of 2026-11-03; red-first overlap, own-failure and queued-waiter tests, then native gates on fix/c1-retain-stop-gate. Stays open until review and merge. C1b (stop admitted after an unprepared start while disposal releases the operations token) is a separate unconfirmed candidate with its own owner, next step and acceptance and is not closed by T018.
- [ ] T019 Cancellation end within 2 s failed on Windows (follow-up-windows-elapsed-bounds.md, finding SDK-WIN-CANCEL-LATENCY-20261104): pull request #28, attempt 1, `EmbeddedConsumerTests:49`. Cause unknown, bound kept, priority P1 (required native check). Owner Alexander Bleissem; next: a stage-timed pure C# diagnostic of the cancellation path on native Windows with a Linux control; reruns and the gate decision are root's. Acceptance: the slow stage is measured or the uncertainty is stated with an envelope, and root decides.
- [ ] T020 Stop deadline within 1 s failed on Windows (follow-up-windows-elapsed-bounds.md, finding SDK-WIN-CLEANUP-LATENCY-20261104): pull request #28, attempt 2, `RuntimeCleanupTests:29`. It is a different test and stage from T019, with no shared cause assumed. Cause unknown, bound kept, priority P1 (required native check). Owner Alexander Bleissem; next: a stage-timed pure C# diagnostic of the stop path (entry, worker completion, cleanup start, deadline firing, return) on native Windows with a Linux control. Acceptance as for T019.
  - Measurements 2026-11-04 (follow-up-windows-elapsed-bounds.md, Measurements): in passing runs on Linux and native Windows the
    T019 path ends in about 120 ms or less and the T020 stop returns within about 15 ms of its 200 ms deadline. The failing runs
    were not measured, so T019 and T020 stay open with their cause unknown.
  - Root decision 2026-11-04: a revised permanent test-only observation (follow-up-windows-elapsed-bounds.md, Permanent test-only
    observation; branch `test/t019-t020-stage-observation`). It is not a runtime fix.
  - Next: root monitoring binds the next naturally failing native run of either test to T019 or T020 with its stage line.
    Owner: this task's owner, with root deciding. No extra runs are started to find a failure.
  - Call-site controls 2026-11-04 (follow-up-windows-elapsed-bounds.md, Call-site failure controls): four private probes. Each
    test was failed once at its early await and once at its original elapsed assertion. The results:
    - each probe kept its primary failure;
    - each wrote one stage line with honest not-observed stages;
    - T020 kept its cleanup order.
    No source change is kept, no bound is changed and no cause is claimed.
  - Natural recurrence 2026-11-04 (follow-up-windows-elapsed-bounds.md, Natural recurrence): bound by root monitoring.
    - Run 41836330209, pull_request, attempt 1, head `91192c5` (pull request #38; documentation-only delta, `src` and `tests`
      identical to `main` `4ab04c4`), job 126596070216 `quality (windows-latest)`.
    - Failure: `RuntimeCleanupTests.cs:39`, the elapsed bound (the line numbering moved from 29 when the observation was added).
    - Stage line: `cleanup-entry-wait-observed-ms=11.4 stop-failure-observed-ms=1101.8`. Root's log is
      `b929fff0…7606`.
    - Not retried. The four runs of that head had 9 successful jobs, 1 failed and 1 skipped.
    - Owner: this task's owner; root decides.
  - Source analysis and private controls 2026-11-04 (same file, Source analysis): the 1101.8 ms is when the test observed the
    failure. It is not a measured runtime interval and not a timer cause.
    - Private Linux controls: under those controls only, the test's observation ran 0.0 to 0.1 ms after a continuation observer
      on the stop task ran (C). C is when that observer executed, not an atomic completion timestamp.
    - On Windows none of the candidate locations is excluded. They are (a) deadline delivery or its dispatch, (b) the runtime
      continuation, (c) the test observer, and (d) a process or host stall. In particular, observer delay and host stall are
      still possible. (Wording corrected 2026-11-04 under NEC-T020-PROP-CLAIM-002; see the follow-up.)
    - A delayed deadline and a starved thread pool leave the same stage line. In the private controls only, two samplers had
      different signatures for them. They are not a cause classifier.
    - No defect is isolated, no Red exists and no fix is proposed. The cause is unknown.
  - Proposal review 2026-11-04 (follow-up, Proposal review and repair). Two opposed reviews and the root disposition accepted
    these findings under this task:
    - NEC-T020-PROP-RESOURCE-001 / DEF-T020-MEASURE-002: the private sampler had no finite lifetime or window. It is a
      proposal-admission blocker, not a product defect.
    - NEC-T020-PROP-CLAIM-002 / DEF-T020-MEASURE-003: the Linux-to-Windows inference was unsupported. Repaired in these
      records.
    - DEF-T020-MEASURE-001: keep the `TimeProvider.System` path. The clock wrapper (D) stays a historical private probe.
      Neither D nor a completion observer (C) is readmitted.
  - Repair 2026-11-04 (follow-up, Proposal review and repair): a bounded, System-preserving, sampler-only private candidate,
    with 10 private controls and 4 detected mutants. Nothing canonical changed.
  - Next (root decides): admit the candidate as a canonical test-only change with its required gates, or keep the current
    line. Owner: this task's owner. Acceptance: unchanged. T019, 008 T006 and 008 T007 stay open.
- [x] T021 Native scenario state-root ownership (follow-up-native-scenario-root-ownership.md, ID
  SDK-NATIVE-SCENARIO-ROOT-OWNERSHIP-20261104). P2. Closed 2026-11-04: the ownership question, not an established product
  defect, was answered by root's own-root decision and pull request #34 (see Closure below).
  - Observation: `NativeScenarioOwnershipTests.ReleasesNativeStateAfterConcreteScenario` creates a
    `multisession-native-scenario-*` root and never deletes it, whereas the 14 theory cases delete theirs. 37 such roots had
    accumulated locally on 2026-11-04.
  - Owner: Alexander Bleissem; root accepts.
  - Next: decide from source and history whether the retention is intended.
  - Dependencies: root's decision.
  - Acceptance: a Red/Green test cleanup of its own root only, passing native Linux and Windows CI, or a documented intentional
    retention. No existing root is deleted as part of it.
  - Root decision 2026-11-04: delete the test's own root after a pass; keep it, with its path reported, after a failure.
  - Implemented on `test/003-t021-native-scenario-root-ownership`:
    - a test-only `OwnedScenarioRoot` that tracks the one fresh root;
    - `ReleasesNativeStateAfterConcreteScenario` keeps every assertion and its order, and closes its probe handle before
      `Finish` runs.
  - Red:
    - 2 of 3 fixture tests failed against a behaviourless scaffold;
    - the unchanged test passed and left one more root (38 to 39).
  - Green: 18 of 18. The root count was unchanged.
  - Private controls:
    - a pass deleted only its own root, and a foreign sentinel stayed;
    - a forced failure kept the original assertion failure, kept the root and reported its path.
  - Mutations, all detected: deleting on failure too, never deleting, no failure report, deleting the parent.
  - Review fix 2026-11-04, NEC-T021-PRIMARY-001 (DEF-T021-001), primary preservation:
    - Red: a failing body with a failing report surfaced `IOException`, not the original failure.
    - Green: the original failure stays first, in an `AggregateException` with the report failure and the kept path.
    - Private controls on the real test:
      - a pass deleted only its own root and kept a foreign sentinel;
      - a failure with a working report kept the original `Assert.Equal` failure and printed the path;
      - a failure with a throwing report gave `AggregateException`: `Assert.Equal` first, `IOException` second, path in the
        message, root kept.
    - Mutations killed:
      - the report failure replaces the primary;
      - the report failure is swallowed;
      - deleting on failure;
      - the path is missing from the message;
      - deleting the parent. This mutation is now caught by the sentinel assertion; earlier it was only caught when the test
        cleanup itself failed.
  - Closure 2026-11-04 (root acceptance):
    - Root accepted head `515d30a` and merged pull request #34 normally as `e6fec23` (parents `558f9b1` and `515d30a`).
    - Gates on `515d30a`: 12 of 12 native checks passed on their first attempt: push quality 41800096712, push guard
      41800096512, PR quality 41800104436 and PR guard 41800104673.
    - Findings closed by that source:
      - SDK-NATIVE-SCENARIO-ROOT-OWNERSHIP-20261104 (own-root deletion after a pass, kept root on failure);
      - NEC-T021-PRIMARY-001 (original failure stays first).
      Both were checked by two opposed reviews and by root.
    - The 42 roots kept on the writer's host are not deleted. Removing any of them is a separate owner-approved action.
    - The historical Red/Green, controls and the recurrence at `4d25064` (spec 008 T006) stay as recorded. That Windows
      timeout is not closed by this task.

T015 evidence: documentation-only head a298bf6, PR run41062018961 and push
run41062007264 passed all tests but reported CrashProtocol.IsKnown and
CrashWorker.RunAsync at zero coverage (CRAP90/210). Earlier receipts remain valid
historical runs, not current-head acceptance. CrashHarness deliberately kills
children, so collector flush cannot be the sole proof of finite error paths.
Direct tests exercise the actual host protocol, malformed store diagnostics and
native lease release; existing deliberate-crash tests remain unchanged. No
production behavior change, threshold change or new architecture is required.

Native run41042889858 verifies T012: 1060 Linux/1066 Windows tests passed;
97.7735%/97.7556% product line coverage passed the unchanged native inventory.
Static run41042889914 passed both operating systems. Those full quality jobs
were red solely for eight unmeasured validation-host changes. The autonomous
correction now measures the complete host; see host-coverage.md. Local final
source f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490
passes 1086 tests and both 87-percent gates (product 97.7735%, expanded Cover
95.5%, zero findings). That result was followed by the retained native timeout/
counter-run and separately reviewed Wave profile. Final source
8d25c9a067989110c00327af988fff8969c35fb601b8100d0e6f07a224f254e9
passed native push41059372988 and PR41059379755, including all four quality/
canonical-consumer jobs, fourteen published scenarios per OS and static checks.
See closeout.md and evidence/wave-budget-native-{push,pr}.json. No historical
Windows root-cause claim follows; follow-up-windows-admission.md remains ready
and feature001 release requirements remain open.

T015 local: worker-protocol-local.json records1100 tests, product97.8053%,
expanded Cover95.7%, no findings. Direct protocol22 cases include all nine valid
barriers, unknown/empty rejection, cooperative/native invalid-store diagnostics,
closed error output and native lease reacquisition. Static489files/509types passed;
semantic reference-resolution limitations remain as previously documented. Current
native PR41065265199 and static41065265423 passed completely at head18a9f1d:
1100 Linux/1106 Windows tests, zero skips; expanded Cover95.7% on both systems;
both native and canonical package consumers and fourteen published scenarios
per OS passed. See evidence/worker-protocol-native-pr.json.

Parallel push41065255148 passed both native test/coverage gates but its published
Windows capacity scenario failed with only generic ExecutionFailed16. The cause
is unknown and is not equated with earlier admission variability. Original failure
and a concrete owned investigation are retained in follow-up-capacity-probe.md.
T016 remains open; no full release or stable published-capacity fix is claimed.

Release 2026-11-04: T017 (pull request #23) and T018 (pull request #24) are contained in 0.2.0-alpha.9, produced by the
`main` run 41738827195 at `67a7de9` and published (receipts in `specs/006-host-execution-authority/evidence/`). Closing
both tasks is the root acceptance record. C1b, the contract Q post-gate survivor, T016 and the Windows follow-ups stay open.

Closure 2026-11-04 (root acceptance of the published 0.2.0-alpha.9): T017 is closed. Contract Q was merged as `dd1ca0e`
(pull request #23) and requalified on the release head. T018 is closed. The retained stop gate was merged as `67a7de9`
(pull request #24). Both are contained in the `main` producer run 41738827195 at `67a7de9` and in publish run 41742503760
(record merge `654563c`, receipt merge `9ac1a8e`). The task texts above keep their wording from implementation time. Still open
and not closed by T017 or T018: C1b and the contract Q post-gate survivor M4 (both with tasks in follow-up-dispose-stop-race.md),
T016, follow-up-windows-admission.md and the Windows 11 release matrix.

Recurrence 2026-11-04: the pull request #30 push run 41769537053 on Windows failed with a setup admission timeout in
`RuntimeLifecycleTests.StopsAcceptanceAndFinalizesAllQueuedPages`. It is bound to follow-up-windows-admission.md (Recurrence
2026-11-04), not to T019 or T020. The pull_request run of the same head passed. Neither run was retried.

Merge receipt 2026-11-04: root merged pull request #30 normally as `451792bc` from head `5454bfe`. On that `main` head, quality
run 41772922424 and codemetr-guard run 41772922429 passed on their first attempt. That pass does not explain the earlier admission
timeout. follow-up-windows-admission.md keeps its owner, next steps and acceptance.

Merge receipt 2026-11-04: root merged pull request #31 normally as `18cbcbba` from head `f832c14`. On that `main` head, quality
run 41776140605 and codemetr-guard run 41776140604 passed on their first attempt.
- Closed by that merge, within their bounded scope only: the review findings NEC-S31-001 and DEF-STAGE-02, about the T019/T020
  call-site controls. They were closed by the actual change, the probes, two narrow opposed reviews and the root merge.
- T019 and T020 stay open with the cause unknown. Root monitoring binds the next naturally failing native TRX stage line, and no
  runs are started to find one.

Admission analysis 2026-11-04: follow-up-windows-admission.md, section "Source analysis and private controls 2026-11-04", and
evidence/windows-default-admission-source-analysis-20261104.md. These hold:
- the source architecture, the TRX comparison and five private offline controls;
- finding ADM-CERTAINTY-QUEUE-20261104;
- measurement proposals M1 to M4 for root decision.
No product, test, bound or pipeline change; no cause is claimed. C1b, Q-M4, T016 and the Windows 11 release matrix keep their
owners and open status.

Merge receipt 2026-11-04: root merged pull request #33 normally as `558f9b1` from head `0083443`. That closes, within their
bounded scope, NEC-M1M2-LOOKUP-001, NEC-M1M2-SNAPSHOT-002 and NEC-ADM-M1-WORDING-002, together with the M1/M2 setup diagnostics.
The Windows default admission cause, T019 and T020 stay open.
