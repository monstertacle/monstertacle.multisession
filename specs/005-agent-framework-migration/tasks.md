# Tasks: Agent Framework migration

## Setup and foundational gates

- [x] T001 Record current source analysis and requirements in specs/005-agent-framework-migration/research.md.
- [x] T002 Complete separate contrary requirements and architecture dispositions in specs/005-agent-framework-migration/research.md.
- [x] T003 Add pinned framework packages and behaviorless public signatures in src/Monstertacle.Multisession.AgentFramework/; update solution, locks and complete coverage inventory.

## US1 and US2: framework execution

- [x] T004 [US1] Observe semantic Red for restoration, final-message selection and awaited observation in tests/Monstertacle.Multisession.UnitTests/FrameworkSessionProviderTests.cs.
- [x] T005 [US2] Observe semantic Red for uncertain failure/cancellation, incompatible state, unsupported actions, incomplete output and serialization failure in tests/Monstertacle.Multisession.UnitTests/FrameworkFailureTests.cs.
- [x] T006 [US1] Implement framework provider and bounded continuation contract in src/Monstertacle.Multisession.AgentFramework/.
- [x] T007 [US2] Verify actual SQLite runtime/restart/custody and terminal serialization race in tests/Monstertacle.Multisession.IntegrationTests/FrameworkRuntimeTests.cs.

## US3: verified evidence renewal

- [x] T008 [US3] Observe semantic Red for dependency invalidation, graph rejection and bounded coordination in tests/Monstertacle.Multisession.UnitTests/EvidenceWorkflowTests.cs.
- [x] T009 [US3] Implement frozen evidence graph and actual typed framework workflow in src/Monstertacle.Multisession.AgentFramework/Evidence/; read associated durable requests only, never submit.
- [x] T010 [US3] Verify missing association, Unknown and restart with same request references cause zero replacement submissions in tests/Monstertacle.Multisession.UnitTests/EvidenceWorkflowTests.cs.

## US4: package migration

- [x] T011 [US4] Rename protocol implementation types/packages to Conversations/Interactions and update references without changing persisted wire IDs in src/, tests/, examples/ and eng/.
- [x] T012 [US4] Add migration guide and package-only generic framework consumer in README.md, docs/ and eng/PackageSmoke.ps1.

## Qualification and completion

- [x] T013 Run full static metrics, format, native quality/coverage and dependency audit; retain exact reports in specs/005-agent-framework-migration/evidence/.
- [ ] T014 Pack and consume artifacts; publish PR and wait for same-source Linux/Windows CI; resolve findings before merge.
- [ ] T015 Record release status and every finding disposition in specs/005-agent-framework-migration/plan.md, merge when qualified and clean only merged migration branches.

## Dependencies and implementation strategy

T001-T003 precede all behavior. US1/US2 tests precede implementation; US3 graph/workflow has independent semantic Red. US4 follows green execution/renewal. Qualification follows all code. Sequential shared-workspace implementation avoids accidental concurrent source mutation. Independent review roles already ran in parallel. No speculative helper tasks.
