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
- [ ] T009 Run full static metrics, native quality/Cover and package consumption.
- [ ] T010 Repeat native Windows scenarios and inspect safe diagnostics for any recurrence.
- [ ] T011 Commit/push reviewable work, bind native CI evidence, disposition all findings.
- [x] T012 Correct UnitTests host target selection after observed Windows foreign-assembly coverage failure; preserve inventory strictness and rerun native CI.
- [ ] T013 Measure the complete validation host in Cover, preserve the separate native product gate, test real native scenario ownership and close measured coverage gaps; see host-coverage.md.

Native run41042889858 verifies T012: 1060 Linux/1066 Windows tests passed;
97.7735%/97.7556% product line coverage passed the unchanged native inventory.
Static run41042889914 passed both operating systems. Those full quality jobs
were red solely for eight unmeasured validation-host changes. The autonomous
correction now measures the complete host; see host-coverage.md. Local final
source f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490
passes 1086 tests and both 87-percent gates (product 97.7735%, expanded Cover
95.5%, zero findings). T009/T013 await the new native CI; T010 requires the
downstream published scenarios. No historical Windows root-cause claim follows
from passing ordinary native suites.
