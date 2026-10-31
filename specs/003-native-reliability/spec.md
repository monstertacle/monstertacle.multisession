# Feature Specification: Native storage reliability
Branch: feature/native-reliability-host-contract
Created: 2026-10-30T06:20:36Z
Status: Done for the bounded implementation; release and latency investigation remain explicitly separate in closeout.md.
Input: Fix the observed Windows failures with SpecKit, and extend this library against current SharpClaw.

## User Scenarios & Testing
### US1 (P1): Operations remain schedulable under host load
Hosts share their worker pool with other components. A pending store read must not wait for an unrelated blocked worker when the store itself is idle.
Acceptance: an isolated process saturates its single managed worker; an idle store read still completes on its owned storage executor, without increasing deadlines or retries. FIFO, bounded capacity, cancellation-before-start, durable commit certainty and cleanup remain unchanged.

### US2 (P1): Actionable, safe failures
A host can distinguish where storage/admission failed without receiving paths, inputs, SQL or exception messages.
Acceptance: real SQL trigger/commit failures preserve closed phase and numeric SQLite codes through request admission and CQRS; admission timeouts identify lookup/commit phase; public exception text remains sanitized.
Scenario reports preserve this evidence rather than replacing it with only StorageUnavailable.

### Edge cases
Already-cancelled work, full queue, lost lease, disposal while queued, failed commit, rollback certainty, primary failure followed by cleanup failure, and existing nullable diagnostic consumers.

## Requirements
- FR-001: Keep one serial owner of each SQLite connection, independent of unrelated managed-worker availability.
- FR-002: Preserve bounded queue, public SDK/runtime time limits and acceptance certainty. The Wave validation consumer explicitly selects a finite admission profile as documented in wave-budget-decision.md; its other deadlines and all default-budget regressions remain unchanged.
- FR-003: Record only closed phase identifiers and numeric native error codes; never raw text or paths.
- FR-004: Preserve optional exception-local diagnostic evidence through normalization and CQRS; leave PublicError and persisted errors unchanged.
- FR-005: Retain original failure evidence when scenario cleanup also fails.
- FR-006: Verify whole native product coverage >=87%, full static metrics and native Linux/Windows CI.
- FR-007: Validate hypotheses before choosing implementation; later successful runs do not disprove earlier failures.

## Key entities
FailureDetails: optional closed phase, SQLite primary/extended code, numeric OS error.
Store executor: bounded FIFO with one owned worker and awaitable completion.

## Success Criteria
SC-001: isolated starvation regression passes without changing deadline.
SC-002: native SQL rollback/commit tests preserve both failure details and original certainty.
SC-003: zero sensitive marker leaks in public/report JSON.
SC-004: full native suites, coverage and package consumption pass on both CI operating systems.

## Assumptions
SharpClaw is a consuming host, with no reverse assembly dependency. Full SharpClaw migration, live API costs and the unavailable Windows11 release host remain separate.
