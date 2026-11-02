# Tasks: Checkpoint base contracts and the delivery checkpoint package

**Input**: spec.md, plan.md, research.md, data-model.md, contracts/checkpoint-contracts.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [x] T001 Preserve the staged pinning tests by merging `feature/015-continuity-pinning` (6df1707) normally (FR-015).
- [x] T002 Move `src/Monstertacle.Multisession.DeliveryCheckpoints` to `src/Monstertacle.Multisession.Checkpoints.Delivery`
  and its tests to `tests/Monstertacle.Multisession.Checkpoints.Delivery.Tests` with history. Change namespaces only; update
  `InternalsVisibleTo`, the solution and lock files. The moved suite must stay green with unchanged assertions (FR-003,
  FR-006, FR-009).
- [x] T003 Create behaviourless scaffolds: projects `src/Monstertacle.Multisession.Checkpoints` (BCL only) and
  `tests/Monstertacle.Multisession.Checkpoints.Tests`, plus the Delivery → base `ProjectReference` (FR-001, FR-004).

## Phase 2: Foundational — inventory guard

- [x] T004 Red: `tests/quality/Test-QualitySuites.Tests.ps1`. A fixture solution with a test project not in the quality suite
  list, or a `tests/*` project missing from the solution, must be refused (FR-012).
- [x] T005 Green: `Get-QualitySuites` in `scripts/Invoke-Quality.ps1` derives the suites from the solution and refuses
  drift; the quality run uses it (FR-012).

## Phase 3: User Story 1 — base contract (P1)

- [x] T006 [US1] Red: base tests in `tests/Monstertacle.Multisession.Checkpoints.Tests`:
  - a typed test double read through `ICheckpoint<T>`;
  - cancellation;
  - single disposal;
  - an architecture test that the base assembly references only BCL assemblies.
- [x] T007 [US1] Green: `src/Monstertacle.Multisession.Checkpoints/ICheckpoint.cs` (FR-001, FR-002, FR-004).
- [x] T008 [US1] Base-only isolated consumer `eng/CheckpointsConsumer.cs.txt` with `eng/Test-CheckpointsConsumer.ps1`. Its
  lock must contain no delivery, SQLite or session package; CI runs it on both platforms (FR-010).

## Phase 4: User Story 2 — unchanged custody (P1)

- [x] T009 [US2] Prove that old-written files are read by the new code: the moved `CurrentFormatTests`, `CurrentGrantTests`
  and `ListenerFixture` keep their fixtures and pass (FR-007, FR-008, SC-002, SC-005).

## Phase 5: User Story 3 — delivery port (P2)

- [x] T010 [US3] Red: `DeliveryPortTests` asserts that `DeliveryCheckpoint` is an `IDeliveryCheckpoint` and an
  `ICheckpoint<CheckpointState>`. A host-shaped loop (`HostShapedLoop`) drives read, stage, sending, preparation failure,
  uncertain and accept only through the port, against the real store. A recording port double proves each call goes through
  the port (bypass detection).
- [x] T011 [US3] Green: `IDeliveryCheckpoint.cs`; `DeliveryCheckpoint : IDeliveryCheckpoint` (FR-005).

## Phase 6: User Story 4 — release set (P2)

- [x] T012 [US4] Update the inventories:
  - `tests/coverage-scope.json`;
  - `eng/Test-PackageLayout.ps1` (nine packages);
  - `eng/Test-DeliveryConsumer.ps1` and `eng/DeliveryConsumer.cs.txt` (new ID and namespace);
  - `.github/workflows/ci.yml` (base consumer);
  - `docs/package-readme.md`.

  Version `0.2.0-alpha.8` (FR-012, FR-014).
- [x] T013 [US4] Documentation: `docs/delivery.md`, `README.md`, `docs/adr/0009-checkpoint-packages.md` superseding ADR 0008
  (FR-016).

## Phase 7: Verification

- [x] T014 Named mutations, each observed red then restored:
  - the base references SQLite;
  - the store drops the port;
  - the loop bypasses the port;
  - a test project is missing from the suites;
  - a package is missing from the layout.
- [ ] T015 Native Linux `scripts/Invoke-Quality.ps1 -Platform linux`, full Codemetr check, pack, layout and both consumers,
  run capped. Hosted Linux and Windows CI on the pull request (FR-013, SC-004).
- [x] T016 Evidence in `specs/013-checkpoint-split/evidence/implementation.md`: commands, Red reasons, Green results,
  mutations and unexecuted checks.

## Dependencies

- T001 → T002 → T003.
- T004 → T005.
- T006 → T007 → T008.
- T010 → T011.
- T012 and T013 after T011.
- T014 to T016 last.
- No task changes the host, publishes a package or edits a release record.
