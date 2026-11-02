# Implementation Plan: Checkpoint base contracts and the delivery checkpoint package

**Branch**: `feature/013-checkpoint-split` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Status**: Phases 0 and 1 are complete. The contract decision of 2026-11-02 closed the architecture gate (spec
Clarifications; contracts/checkpoint-contracts.md). The version is proposed as `0.2.0-alpha.8` (research R6). That version
is not reserved until it is published.

## Summary

The existing `Monstertacle.Multisession.DeliveryCheckpoints` package is split into two packages:

- a storage-independent base, `Monstertacle.Multisession.Checkpoints`, with a strongly typed read and lifetime contract;
- `Monstertacle.Multisession.Checkpoints.Delivery`, which holds every delivery type, the concrete SQLite checkpoint, its
  grants and its typed refusals, and implements a delivery port extending the base contract.

The persisted format and the behaviour stay unchanged (research R3). There are no aliases, forwarders or format migration.
The package set moves to a new, unused version.

## Technical Context

**Language/Version**: C# 14, .NET SDK 10.0.403 (`global.json`), target `net10.0`

**Primary Dependencies**: base: none beyond the BCL. Delivery: `Microsoft.Data.Sqlite` (central version) and the base project.

**Storage**: unchanged SQLite checkpoint format version 1 (ADR 0007) in Delivery only

**Testing**: xUnit with the existing coverage runsettings; two test projects (base, Delivery); isolated package consumers
in `eng/`

**Target Platform**: Linux and Windows, native quality on both

**Project Type**: libraries (NuGet packages)

**Performance Goals**: unchanged; no new I/O path

**Constraints**:
- 87 % product line coverage per platform;
- full Codemetr check with unchanged pins and no baseline;
- no Python;
- alpha.7 bytes stay immutable.

**Scale/Scope**: about 28 source files move; two new projects, two new test projects; scripts and docs updated

## Constitution Check

| Principle | Check | Status |
| --- | --- | --- |
| I. C#/.NET, no Python, async, cancellation | Unchanged implementation; cancellation contract carried into the port | pass |
| II. Deep dive before extension | research.md with source hash, consumers, format and tests | pass |
| II. Unresolved architecture question blocks implementation | Final contract shape decided 2026-11-02 | pass |
| II. SOLID/ISP/DIP | Base read/lifetime only; delivery port for transitions; grants concrete (connection-bound) | pass (decided contract) |
| II. DRY, no anti-patterns | Moves without copies; no forwarders | pass |
| III. Codemetr | Full check on all moved and new code; no pin, threshold or baseline change | planned |
| IV. TDD | Red-first for new contracts, the inventory guard, the base consumer and the architecture test; moved tests stay green with unchanged assertions | planned |
| IV. 87 % per platform, complete scope | Both assemblies in `tests/coverage-scope.json`; the inventory check exists | planned |
| V. Security | Private-access tests move unchanged | planned |
| VI. English | All new content English | pass |
| Release process | ADR superseding 0008; release record only after a qualifying `main` CI run; no publication in this change | planned |

## Project Structure

### Documentation (this feature)

```text
specs/013-checkpoint-split/
├── spec.md
├── checklists/requirements.md
├── research.md
├── data-model.md
├── contracts/checkpoint-contracts.md
├── quickstart.md
└── plan.md
```

`tasks.md` follows the contract decision.

### Source Code (repository root)

```text
src/Monstertacle.Multisession.Checkpoints/                     # new: base contract(s)
src/Monstertacle.Multisession.Checkpoints.Delivery/            # moved from DeliveryCheckpoints, new namespace
tests/Monstertacle.Multisession.Checkpoints.Tests/             # new: contract and architecture tests
tests/Monstertacle.Multisession.Checkpoints.Delivery.Tests/    # moved delivery tests and fixtures, shared port cases
eng/Test-CheckpointsConsumer.ps1, eng/CheckpointsConsumer.cs.txt   # new base-only isolated consumer
eng/Test-DeliveryConsumer.ps1, eng/DeliveryConsumer.cs.txt     # updated package name and namespaces
eng/Test-PackageLayout.ps1, scripts/Invoke-Quality.ps1, tests/coverage-scope.json, Monstertacle.Multisession.slnx,
.github/workflows/ci.yml                                       # inventories
docs/delivery.md, docs/package-readme.md, README.md, docs/adr/0009-checkpoint-packages.md
```

**Structure Decision**: two projects under `src/`. Delivery holds a `ProjectReference` to the base. Both are packed.

## Red-first units (to be confirmed by tasks)

| Unit | Test written first | Expected Red | Minimal Green |
| --- | --- | --- | --- |
| Suite inventory guard | `tests/quality` case: a solution test project missing from the list | Quality accepts the omission | Compare the suite list with the solution tests |
| Base independence | Architecture test on the base assembly's references; base consumer lock check | Fails after a probe reference is added | No reference |
| Base contract | Typed double cases: read, cancellation, disposal | Contract missing (an executed assertion, not only a compile failure) | Interface |
| Delivery port | Shared cases against the real store and a double | Real store does not implement the port | `DeliveryCheckpoint : port` |
| Package set | Layout check with nine names | Eight-package layout fails | Pack both |

Named mutations: base references SQLite; the store drops the port; a test project is removed from the suite list; a package is
removed from the layout; the delivery consumer still imports the old namespace.

## Complexity Tracking

No violations to justify.
