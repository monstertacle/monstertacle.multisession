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
