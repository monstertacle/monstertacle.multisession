# Tasks: CQRS and coverage
Created: 2026-10-30T01:21:40Z

## Setup
- [x] T015 Align unpublished package versions with the required prerelease dependency and verify locked external consumption.
- [x] T001 Inspect architecture and published dependency; record current source and decisions in analysis.md.
- [x] T002 Write specification, plan, contracts and acceptance tasks before implementation.
- [x] T003 Pin SharpQrs and Cover; validate tool discovery/configuration and package source mapping.

## US1 typed dispatch (depends on T003)
- [x] T004 Add public request contract stubs and behavioral contract tests; observe and archive real Red.
- [x] T005 Implement typed handlers, non-null envelopes and immutable instance-local dispatch.
- [x] T006 Integrate legacy facades and runtime composition without duplicating lifecycle rules.

## US2 compatibility (depends on T006)
- [x] T013 Fix observed shutdown continuation regression; drain final public dispatcher tasks and test typed/legacy recovery.
- [x] T007 Verify all fourteen mappings, token/error behavior, isolation, lifecycle and existing tests.
- [x] T008 Update package-only consumption and documentation; verify six packages and transitive dependency.

## US3 quality (depends on T007)
- [x] T014 Resolve measured static metric findings through responsibility splits on a green behavioral baseline.
- [ ] T009 Integrate full Codemetr Cover check into native quality CI; retain 87% aggregate gate and source hashing.
- [ ] T010 Run full static, format, locked restore, audit, native tests and coverage; inspect complete report.
- [ ] T011 Commit and push; obtain native Linux and Windows CI results for final source.
- [ ] T012 Record evidence, self-review and disposition of any findings; update task status honestly.

