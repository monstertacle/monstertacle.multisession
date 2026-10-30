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
- [ ] T015 Close current-head Windows CRAP gaps with deterministic direct worker protocol tests in WorkerProtocolTests.cs and repeat complete native CI without gate changes.

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

T015 local: worker-protocol-local.json records1100 tests, product97.7735%,
expanded Cover95.6%, no findings. Direct protocol22 cases include all nine valid
barriers, unknown/empty rejection, cooperative/native invalid-store diagnostics,
closed error output and native lease reacquisition. Static489files/509types passed;
semantic reference-resolution limitations remain as previously documented. Current
native CI acceptance is pending the new test commit.
