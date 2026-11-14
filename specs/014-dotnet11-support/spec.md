# Feature Specification: additional .NET 11 support

**Created**: 2026-11-14T09:57:24Z (measured with `date -u`)
**Authority**: owner's request for the entire product family, retaining .NET 10.

## User scenarios

A host embeds the nine libraries on either supported runtime and its actual native
OS. Existing sessions, state, leases, checkpoints, CQRS and provider selection retain
their contracts. A maintainer qualifies every target independently before release.

## Requirements and acceptance

- FR-001: All nine libraries and six test suites support .NET 10 and .NET 11;
  Windows-specific projects retain their matching windows target suffix.
- FR-002: Every test and validation child executes the selected target. Each native
  OS/runtime combination separately meets the existing 87% product floor and complete
  coverage guard. Never aggregate target results or count foreign-platform success.
- FR-003: Each package contains both matching assets and dependency groups, preserving
  metadata, MIT notices and public dependency direction. Native public, framework,
  checkpoint and delivery consumers run on both runtimes using identical package bytes.
- FR-004: Locked restore, analyzers, full static metrics, audit, native publish and
  Linux/Windows CI pass without lowered thresholds, new exclusions or baselines.
- FR-005: Existing state/API/package identities and .NET 10 use stay compatible.
  No new provider capability, external session adoption or package publication is claimed.

## Success criteria and clarifications

Both runtimes pass native coverage and actual consumer/host scenarios on both OSes.
Missing targets, skipped suites and mismatched child binaries cannot qualify success.
No unresolved scope question; the existing constitution delegates SDK choice to the plan.
